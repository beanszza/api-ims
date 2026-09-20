using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Api.Contracts.Production;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class ProductionService : IProductionService
{
    private readonly ScmDbContext _context;
    private readonly IStockPostingService _stockPosting;
    private readonly IAllocationService _allocation;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IStatusTransitionGuard _statusGuard;
    private readonly IUomConversionService _uomConversion;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocationResolver _locations;
    private readonly IAuditTrail _audit;
    private readonly ILogger<ProductionService> _logger;

    public ProductionService(
        ScmDbContext context,
        IStockPostingService stockPosting,
        IAllocationService allocation,
        IDocumentNumberService documentNumbers,
        IStatusTransitionGuard statusGuard,
        IUomConversionService uomConversion,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        ILocationResolver locations,
        IAuditTrail audit,
        ILogger<ProductionService> logger)
    {
        _context = context;
        _stockPosting = stockPosting;
        _allocation = allocation;
        _documentNumbers = documentNumbers;
        _statusGuard = statusGuard;
        _uomConversion = uomConversion;
        _posting = posting;
        _currentUser = currentUser;
        _locations = locations;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ProductionBatchResponse> CreateBatchAsync(CreateProductionBatchRequest request)
    {
        var recipe = await _context.Recipes
            .Include(r => r.RecipeIngredients)
            .Include(r => r.Product).ThenInclude(p => p!.Item)
            .FirstOrDefaultAsync(r => r.RecipeId == request.RecipeId);

        if (recipe == null) throw new InvalidOperationException("Recipe not found.");
        if (recipe.ProductId != request.ProductId) throw new InvalidOperationException("Recipe does not match the selected product.");

        var estimatedQuantity = recipe.OutputQuantity * request.BatchMultiplier;
        var batchNumber = await _documentNumbers.NextAsync(DocumentType.ProductionOrder, request.ScheduleDate);

        var batch = new ProductionBatch
        {
            BatchNumber = batchNumber,
            RecipeId = request.RecipeId,
            ProductId = request.ProductId,
            BatchMultiplier = request.BatchMultiplier,
            ProductionDate = request.ScheduleDate,
            EstimatedQuantity = estimatedQuantity,
            ActualQuantity = 0,
            Stage = ProductionStage.Preparation,
            Status = BatchStatus.Scheduled,
            AssignedCook = request.AssignedCook,
            QualityStatus = QcStatus.Pending
        };

        _context.ProductionBatches.Add(batch);
        await _context.SaveChangesAsync();

        _audit.Record(
            nameof(ProductionBatch),
            batch.BatchNumber,
            "BatchCreated",
            fieldName: "Status",
            oldValue: null,
            newValue: "Scheduled");

        return MapToResponse(batch, recipe.RecipeName, recipe.Product?.Item?.ItemName ?? "");
    }

    public async Task<IEnumerable<ProductionBatchResponse>> GetAllBatchesAsync()
    {
        var batches = await _context.ProductionBatches
            .Include(b => b.Recipe).ThenInclude(r => r!.Product).ThenInclude(p => p!.Item)
            .Include(b => b.FgLot)
            .OrderByDescending(b => b.BatchId)
            .ToListAsync();

        return batches.Select(b => MapToResponse(b, b.Recipe?.RecipeName ?? "", b.Recipe?.Product?.Item?.ItemName ?? ""));
    }

    public async Task<ProductionBatchResponse> UpdateStageAsync(int batchId, UpdateStageRequest request)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe).ThenInclude(r => r!.RecipeIngredients)
            .Include(b => b.Recipe).ThenInclude(r => r!.Product).ThenInclude(p => p!.Item)
            .Include(b => b.FgLot)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch not found.");

        if (!EnumDbValue.TryParse<ProductionStage>(request.Stage, out var requestedStage)
            || requestedStage == ProductionStage.Unspecified)
        {
            throw new InvalidOperationException(
                $"Invalid stage '{request.Stage}'. Accepted values: {EnumDbValue.DescribeAccepted<ProductionStage>()}.");
        }

        var isQaCheckpoint = requestedStage is ProductionStage.QaReview or ProductionStage.QualityControl;

        bool isStarting = batch.Status == BatchStatus.Scheduled
            && !isQaCheckpoint
            && requestedStage != ProductionStage.Completed
            && requestedStage != ProductionStage.Cancelled;

        bool isCompleting = requestedStage == ProductionStage.Completed && batch.Status != BatchStatus.Completed;

        batch.Stage = requestedStage;
        if (request.ActualQuantity.HasValue && request.ActualQuantity.Value > 0)
        {
            batch.ActualQuantity = request.ActualQuantity.Value;
        }

        var targetStatus = requestedStage switch
        {
            ProductionStage.Completed => BatchStatus.Completed,
            ProductionStage.Cancelled => BatchStatus.Cancelled,
            _ when isQaCheckpoint => batch.Status,
            _ => BatchStatus.InProgress
        };

        _statusGuard.EnsureCanTransition(batch.Status, targetStatus);
        batch.Status = targetStatus;

        var actor = _currentUser.Current;
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);

        await _posting.ExecuteAsync(async () =>
        {
            // 1. DEDUCT INGREDIENTS ON BATCH START VIA FEFO
            if (isStarting)
            {
                var warehouseLocation = await _locations.RequireSystemLocationAsync(LocationType.Warehouse);
                var warehouseId = warehouseLocation.LocationId;

                var recipe = batch.Recipe;
                if (recipe != null)
                {
                    decimal totalMaterialCost = 0m;
                    var drawsToConsume = new List<LotDraw>();
                    var consumptions = new List<BatchConsumption>();

                    foreach (var ingredient in recipe.RecipeIngredients)
                    {
                        var recipeQuantity = ingredient.StandardQuantity * batch.BatchMultiplier;
                        var requiredQuantity = await _uomConversion.ConvertToItemStockUomAsync(
                            recipeQuantity, ingredient.UomId, ingredient.ItemId);

                        var allocationResult = await _allocation.AllocateFefoAsync(
                            ingredient.ItemId, warehouseId, requiredQuantity);

                        if (!allocationResult.IsFulfilled)
                        {
                            var item = await _context.Items.FindAsync(ingredient.ItemId);
                            throw InsufficientStockException.ForLocation(
                                item?.ItemName ?? $"Item {ingredient.ItemId}",
                                requiredQuantity,
                                allocationResult.AllocatedQuantity);
                        }

                        foreach (var alloc in allocationResult.Allocations)
                        {
                            drawsToConsume.Add(new LotDraw(alloc.LotId, alloc.QuantityToDraw));
                            var lineCost = alloc.QuantityToDraw * alloc.UnitCost;
                            totalMaterialCost += lineCost;

                            consumptions.Add(new BatchConsumption
                            {
                                BatchId = batch.BatchId,
                                ItemId = ingredient.ItemId,
                                LotId = alloc.LotId,
                                RequiredQuantity = requiredQuantity,
                                QuantityUsed = alloc.QuantityToDraw,
                                UnitCost = alloc.UnitCost,
                                UomId = ingredient.UomId
                            });
                        }
                    }

                    // Execute physical lot draw
                    await _stockPosting.ConsumeAsync(
                        drawsToConsume,
                        MovementType.ProductionConsumption,
                        nameof(ProductionBatch),
                        batch.BatchNumber,
                        $"Raw material & packaging consumption for Batch {batch.BatchNumber}");

                    batch.TotalMaterialCost = totalMaterialCost;
                    _context.BatchConsumptions.AddRange(consumptions);
                }
            }

            // 2. STAGE FINISHED GOODS LOT IN QUARANTINE ON COMPLETION
            if (isCompleting)
            {
                var actualQty = batch.ActualQuantity > 0 ? batch.ActualQuantity : batch.EstimatedQuantity;
                batch.ActualQuantity = actualQty;
                batch.CompletedDate = now;

                if (batch.EstimatedQuantity > 0)
                {
                    batch.YieldPercentage = Math.Round((actualQty / batch.EstimatedQuantity) * 100m, 2);
                }

                if (actualQty > 0)
                {
                    batch.UnitCost = Math.Round(batch.TotalMaterialCost / actualQty, 4);
                }

                var fgLocation = await _locations.RequireSystemLocationAsync(LocationType.FinishedGoods);
                var actualItemId = batch.Recipe?.Product?.ItemId ?? 0;

                if (actualItemId == 0)
                {
                    var product = await _context.FinishedProducts.FindAsync(batch.ProductId);
                    actualItemId = product?.ItemId ?? 0;
                }

                if (actualItemId > 0)
                {
                    // Finished goods lot is created in Quarantine status
                    var receiveRequest = new ReceiveLotRequest(
                        ItemId: actualItemId,
                        LocationId: fgLocation.LocationId,
                        Quantity: actualQty,
                        SourceType: LotSourceType.Produced,
                        Status: LotStatus.Quarantine)
                    {
                        ProductionOrderId = batch.BatchId,
                        ManufactureDate = today,
                        ExpiryDate = today.AddMonths(12),
                        UnitCost = batch.UnitCost,
                        ReferenceType = nameof(ProductionBatch),
                        ReferenceId = batch.BatchNumber,
                        Notes = $"Manufactured batch output: {batch.BatchNumber}"
                    };

                    var fgLot = await _stockPosting.ReceiveAsync(receiveRequest);
                    batch.FgLot = fgLot;
                }
            }

            _context.ProductionBatches.Update(batch);

            _audit.Record(
                nameof(ProductionBatch),
                batch.BatchNumber,
                "StageUpdated",
                fieldName: "Stage",
                oldValue: null,
                newValue: EnumDbValue.ToDbValue(requestedStage));

            return batch;
        });

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> SubmitQaAsync(int batchId, UpdateQaNotesRequest request)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe).ThenInclude(r => r!.Product).ThenInclude(p => p!.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch not found.");

        batch.Notes = request.Notes;
        await _context.SaveChangesAsync();

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> UpdateQaApprovalAsync(int batchId, UpdateQaApprovalRequest request)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe).ThenInclude(r => r!.Product).ThenInclude(p => p!.Item)
            .Include(b => b.FgLot)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch not found.");

        var qaVerdict = request.IsApproved ? QcStatus.Approved : QcStatus.Rejected;
        var batchOutcome = request.IsApproved ? BatchStatus.PassedQa : BatchStatus.Rejected;

        _statusGuard.EnsureCanTransition(batch.QualityStatus, qaVerdict);
        _statusGuard.EnsureCanTransition(batch.Status, batchOutcome);

        await _posting.ExecuteAsync(async () =>
        {
            batch.QualityStatus = qaVerdict;
            batch.Status = batchOutcome;

            if (request.IsApproved)
            {
                batch.Stage = ProductionStage.Packaging;

                // Release Finished Goods Lot from Quarantine to Available
                if (batch.FgLot != null)
                {
                    batch.FgLot.Status = LotStatus.Available;
                    _context.InventoryLots.Update(batch.FgLot);

                    // Credit available FG stock cache
                    var inv = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ItemId == batch.FgLot.ItemId && i.LocationId == batch.FgLot.LocationId);

                    if (inv == null)
                    {
                        _context.Inventories.Add(new Inventory
                        {
                            ItemId = batch.FgLot.ItemId,
                            LocationId = batch.FgLot.LocationId,
                            CurrentStock = batch.FgLot.QuantityRemaining
                        });
                    }
                    else
                    {
                        inv.CurrentStock += batch.FgLot.QuantityRemaining;
                        _context.Inventories.Update(inv);
                    }
                }
            }
            else
            {
                batch.RejectionReason = request.RejectionReason;

                if (batch.FgLot != null)
                {
                    batch.FgLot.Status = LotStatus.Rejected;
                    _context.InventoryLots.Update(batch.FgLot);
                }
            }

            _context.ProductionBatches.Update(batch);

            _audit.Record(
                nameof(ProductionBatch),
                batch.BatchNumber,
                "QualityVerdictRecorded",
                fieldName: "QualityStatus",
                oldValue: "Pending",
                newValue: EnumDbValue.ToDbValue(qaVerdict));

            return batch;
        });

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> AddBatchToInventoryAsync(int batchId)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe).ThenInclude(r => r!.Product).ThenInclude(p => p!.Item)
            .Include(b => b.FgLot)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch not found.");

        if (batch.Status != BatchStatus.PassedQa && batch.Status != BatchStatus.Completed)
            throw new InvalidOperationException("Batch must pass QA inspection before adding to available inventory.");

        batch.Status = BatchStatus.InventoryAdded;
        await _context.SaveChangesAsync();

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> UploadImageAsync(int batchId, IFormFile file)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe).ThenInclude(r => r!.Product).ThenInclude(p => p!.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new InvalidOperationException("Batch not found.");

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "batches");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        batch.ImageUrl = $"/uploads/batches/{uniqueFileName}";
        await _context.SaveChangesAsync();

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var active = await _context.ProductionBatches.CountAsync(b => b.Status == BatchStatus.InProgress);
        var delayed = await _context.ProductionBatches.CountAsync(b => b.Status == BatchStatus.Scheduled && b.ProductionDate < now);
        var passedQa = await _context.ProductionBatches.CountAsync(b => b.Status == BatchStatus.PassedQa);
        var completed = await _context.ProductionBatches.CountAsync(b => b.Status == BatchStatus.Completed || b.Status == BatchStatus.InventoryAdded);

        return new DashboardSummaryResponse
        {
            ActiveBatches = active,
            DelayedBatches = delayed,
            PassedQaBatches = passedQa,
            CompletedBatches = completed
        };
    }

    public async Task<IEnumerable<LowStockAlertResponse>> GetLowStockAlertsAsync()
    {
        var rawMaterials = await _context.Items
            .Include(i => i.Category)
            .Where(i => i.Category != null && i.Category.CategoryName == "Raw Material")
            .ToListAsync();

        var alerts = new List<LowStockAlertResponse>();

        foreach (var item in rawMaterials)
        {
            var totalStock = await _context.Inventories
                .Where(inv => inv.ItemId == item.ItemId)
                .SumAsync(inv => inv.CurrentStock);

            if (totalStock <= item.MinStockLevel)
            {
                alerts.Add(new LowStockAlertResponse
                {
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    CurrentStock = totalStock,
                    MinStockLevel = item.MinStockLevel
                });
            }
        }

        return alerts;
    }

    private static ProductionBatchResponse MapToResponse(ProductionBatch b, string recipeName, string productName) => new()
    {
        BatchId = b.BatchId,
        BatchNumber = b.BatchNumber,
        RecipeId = b.RecipeId,
        RecipeName = recipeName,
        ProductId = b.ProductId,
        ProductName = productName,
        BatchMultiplier = b.BatchMultiplier,
        EstimatedQuantity = b.EstimatedQuantity,
        ActualQuantity = b.ActualQuantity,
        ProductionDate = b.ProductionDate,
        Stage = EnumDbValue.ToDbValue(b.Stage),
        Status = EnumDbValue.ToDbValue(b.Status),
        AssignedCook = b.AssignedCook,
        QualityStatus = EnumDbValue.ToDbValue(b.QualityStatus),
        RejectionReason = b.RejectionReason,
        ImageUrl = b.ImageUrl,
        Notes = b.Notes
    };
}
