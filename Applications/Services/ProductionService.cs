using Api.Contracts.Production;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace Applications.Services;

public class ProductionService : IProductionService
{
    private readonly ScmDbContext _context;
    private readonly IStatusTransitionGuard _statusGuard;
    private readonly IUomConversionService _uomConversion;
    private readonly IPostingTransaction _posting;

    public ProductionService(
        ScmDbContext context,
        IStatusTransitionGuard statusGuard,
        IUomConversionService uomConversion,
        IPostingTransaction posting)
    {
        _context = context;
        _statusGuard = statusGuard;
        _uomConversion = uomConversion;
        _posting = posting;
    }

    public async Task<ProductionBatchResponse> CreateBatchAsync(CreateProductionBatchRequest request)
    {
        var recipe = await _context.Recipes
            .Include(r => r.RecipeIngredients)
            .FirstOrDefaultAsync(r => r.RecipeId == request.RecipeId);

        if (recipe == null) throw new Exception("Recipe not found.");
        if (recipe.ProductId != request.ProductId) throw new Exception("Recipe does not match the selected product.");

        // Inventory will be deducted when production starts (UpdateStageAsync)

        var estimatedQuantity = recipe.OutputQuantity * request.BatchMultiplier;

        var batch = new ProductionBatch
        {
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

        return MapToResponse(batch, recipe.RecipeName, recipe.Product?.Item?.ItemName ?? "");
    }

    public async Task<IEnumerable<ProductionBatchResponse>> GetAllBatchesAsync()
    {
        var batches = await _context.ProductionBatches
            .Include(b => b.Recipe)
            .ThenInclude(r => r.Product)
            .ThenInclude(p => p.Item)
            .ToListAsync();

        return batches.Select(b => MapToResponse(b, b.Recipe?.RecipeName ?? "", b.Recipe?.Product?.Item?.ItemName ?? ""));
    }

    public async Task<ProductionBatchResponse> UpdateStageAsync(int batchId, UpdateStageRequest request)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe)
            .ThenInclude(r => r.Product)
            .ThenInclude(p => p.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new Exception("Batch not found.");

        if (!EnumDbValue.TryParse<ProductionStage>(request.Stage, out var requestedStage)
            || requestedStage == ProductionStage.Unspecified)
        {
            throw new Exception(
                $"Invalid stage '{request.Stage}'. Accepted values: {EnumDbValue.DescribeAccepted<ProductionStage>()}.");
        }

        // The QA checkpoint is spelled two different ways by two different screens
        // ("Quality Control" on the production board, "QA Review" in the upload modal). Treat both
        // as the same checkpoint until Task 29 unifies the vocabulary.
        var isQaCheckpoint = requestedStage is ProductionStage.QaReview or ProductionStage.QualityControl;

        bool isStarting = batch.Status == BatchStatus.Scheduled
            && !isQaCheckpoint
            && requestedStage != ProductionStage.Completed
            && requestedStage != ProductionStage.Cancelled;

        batch.Stage = requestedStage;
        if (request.ActualQuantity.HasValue && request.ActualQuantity.Value > 0)
        {
            batch.ActualQuantity = request.ActualQuantity.Value;
        }

        var targetStatus = requestedStage switch
        {
            ProductionStage.Completed => BatchStatus.Completed,
            ProductionStage.Cancelled => BatchStatus.Cancelled,
            _ when isQaCheckpoint => batch.Status, // reaching QA does not itself change the status
            _ => BatchStatus.InProgress
        };

        _statusGuard.EnsureCanTransition(batch.Status, targetStatus);
        batch.Status = targetStatus;

        // Starting a batch deducts every ingredient. Either the whole set of deductions and the stage
        // change land, or none of them do: a shortage discovered on the fifth ingredient must not leave
        // the first four already taken out of stock.
        await _posting.ExecuteAsync(async () =>
        {
        if (isStarting)
        {
            // Deduct inventory
            var recipe = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                .FirstOrDefaultAsync(r => r.RecipeId == batch.RecipeId);

            if (recipe != null)
            {
                foreach (var ingredient in recipe.RecipeIngredients)
                {
                    // The recipe states its quantity in its own unit, which need not be the unit the
                    // item is stocked in. Convert before touching a balance: an ingredient written as
                    // 10 000 g must take 10 kg off a kilogram balance, not 10 000.
                    var recipeQuantity = ingredient.StandardQuantity * batch.BatchMultiplier;
                    var requiredQuantity = await _uomConversion.ConvertToItemStockUomAsync(
                        recipeQuantity, ingredient.UomId, ingredient.ItemId);

                    var inventories = await _context.Inventories
                        .Where(i => i.ItemId == ingredient.ItemId && i.CurrentStock > 0)
                        .OrderBy(i => i.LocationId)
                        .ToListAsync();

                    decimal remainingToDeduct = requiredQuantity;
                    foreach (var inv in inventories)
                    {
                        if (remainingToDeduct <= 0) break;

                        decimal deductAmount = Math.Min(inv.CurrentStock, remainingToDeduct);
                        inv.CurrentStock -= deductAmount;
                        remainingToDeduct -= deductAmount;

                        _context.InventoryMovementLogs.Add(new InventoryMovementLog
                        {
                            ItemId = ingredient.ItemId,
                            LocationId = inv.LocationId,
                            ActionType = "OUT",
                            ChangeQuantity = -deductAmount, // deduction
                            ReferenceId = $"Production Consumption - Batch {batch.BatchId}",
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    if (remainingToDeduct > 0)
                    {
                        var item = await _context.Items
                            .Include(i => i.StockUom)
                            .FirstOrDefaultAsync(i => i.ItemId == ingredient.ItemId);
                        var unit = item?.StockUom?.Abbreviation ?? string.Empty;
                        throw new Exception(
                            $"Insufficient stock for ingredient {item?.ItemName ?? ingredient.ItemId.ToString()}. " +
                            $"Required: {requiredQuantity}{unit}, Short by: {remainingToDeduct}{unit}");
                    }
                }
            }
        }
        });

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> SubmitQaAsync(int batchId, UpdateQaNotesRequest request)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe)
            .ThenInclude(r => r.Product)
            .ThenInclude(p => p.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new Exception("Batch not found.");

        batch.Notes = request.Notes;
        await _context.SaveChangesAsync();

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> UpdateQaApprovalAsync(int batchId, UpdateQaApprovalRequest request)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe)
            .ThenInclude(r => r.Product)
            .ThenInclude(p => p.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new Exception("Batch not found.");

        var qaVerdict = request.IsApproved ? QcStatus.Approved : QcStatus.Rejected;
        var batchOutcome = request.IsApproved ? BatchStatus.PassedQa : BatchStatus.Rejected;

        _statusGuard.EnsureCanTransition(batch.QualityStatus, qaVerdict);
        _statusGuard.EnsureCanTransition(batch.Status, batchOutcome);

        batch.QualityStatus = qaVerdict;
        batch.Status = batchOutcome;

        if (request.IsApproved)
        {
            batch.Stage = ProductionStage.Packaging;
        }
        else
        {
            batch.RejectionReason = request.RejectionReason;
        }

        await _context.SaveChangesAsync();
        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> AddBatchToInventoryAsync(int batchId)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe)
                .ThenInclude(r => r.Product)
                    .ThenInclude(p => p.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new Exception("Batch not found.");
        if (batch.Status == BatchStatus.InventoryAdded) throw new Exception("Batch already added to inventory.");

        _statusGuard.EnsureCanTransition(batch.Status, BatchStatus.InventoryAdded);

        // Posting finished goods touches locations, categories, master data, a balance and the ledger.
        // Previously each of those was saved separately, so a failure part way through could leave the
        // stock added but the batch still unposted, or the reverse.
        await _posting.ExecuteAsync(async () =>
        {
        // Determine actual quantity produced - assuming estimated for now if not set
        decimal actualQty = batch.ActualQuantity > 0 ? batch.ActualQuantity : batch.EstimatedQuantity;

        // Add finished goods to inventory. Fetch or create the "Finished Goods" location.
        var finishedGoodsLocation = await _context.Locations
            .FirstOrDefaultAsync(l => l.LocationName == "Finished Goods" || l.LocationName.ToLower().Contains("finished"));
            
        if (finishedGoodsLocation == null)
        {
            finishedGoodsLocation = new Domains.Entities.Location
            {
                LocationName = "Finished Goods",
                Address = "Main Plant",
                Status = "Active"
            };
            _context.Locations.Add(finishedGoodsLocation);
            await _context.SaveChangesAsync();
        }
        var locationId = finishedGoodsLocation.LocationId;
        
        // Determine actual item ID from the finished product or recipe
        int actualItemId = 0;
        if (batch.Recipe?.Product?.ItemId > 0)
        {
            actualItemId = batch.Recipe.Product.ItemId;
        }
        else
        {
            var product = await _context.FinishedProducts.Include(fp => fp.Item).FirstOrDefaultAsync(fp => fp.ProductId == batch.ProductId);
            if (product?.ItemId > 0)
            {
                actualItemId = product.ItemId;
            }
        }

        if (actualItemId == 0) throw new Exception("Item ID not found for this product.");

        // Ensure the item is categorized under Finished Good so tab queries find it
        var finishedGoodCategory = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryName.ToLower().Contains("finished good"));

        if (finishedGoodCategory == null)
        {
            finishedGoodCategory = new Domains.Entities.Category { CategoryName = "Finished Good", Description = "Finished Goods" };
            _context.Categories.Add(finishedGoodCategory);
            await _context.SaveChangesAsync();
        }

        var item = await _context.Items.FirstOrDefaultAsync(i => i.ItemId == actualItemId);
        if (item != null)
        {
            item.CategoryId = finishedGoodCategory.CategoryId;
            await _context.SaveChangesAsync();
        }

        var existingInventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ItemId == actualItemId && i.LocationId == locationId);
            
        if (existingInventory != null)
        {
            existingInventory.CurrentStock += actualQty;
        }
        else
        {
            _context.Inventories.Add(new Inventory
            {
                ItemId = actualItemId,
                LocationId = locationId,
                CurrentStock = actualQty,
                DriverId = null
            });
        }

        _context.InventoryMovementLogs.Add(new InventoryMovementLog
        {
            ItemId = actualItemId,
            LocationId = locationId,
            ActionType = "IN",
            ChangeQuantity = actualQty,
            ReferenceId = $"Production Batch {batchId}",
            Timestamp = DateTime.UtcNow
        });

        batch.Status = BatchStatus.InventoryAdded;
        });

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<ProductionBatchResponse> UploadImageAsync(int batchId, IFormFile file)
    {
        var batch = await _context.ProductionBatches
            .Include(b => b.Recipe)
            .ThenInclude(r => r.Product)
            .ThenInclude(p => p.Item)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null) throw new Exception("Batch not found.");

        if (file == null || file.Length == 0) throw new Exception("No file was uploaded.");

        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            string base64String = Convert.ToBase64String(fileBytes);
            batch.ImageUrl = $"data:{file.ContentType};base64,{base64String}";
        }

        await _context.SaveChangesAsync();

        return MapToResponse(batch, batch.Recipe?.RecipeName ?? "", batch.Recipe?.Product?.Item?.ItemName ?? "");
    }

    public async Task<DashboardSummaryResponse> GetDashboardSummaryAsync()
    {
        var active = await _context.ProductionBatches.CountAsync(b =>
            b.Status == BatchStatus.InProgress || b.Status == BatchStatus.Scheduled);
        var delayed = await _context.ProductionBatches.CountAsync(b =>
            b.ProductionDate < DateTime.UtcNow
            && b.Status != BatchStatus.Completed
            && b.Status != BatchStatus.Rejected
            && b.Status != BatchStatus.InventoryAdded);
        var passedQa = await _context.ProductionBatches.CountAsync(b => b.Status == BatchStatus.PassedQa);
        var completed = await _context.ProductionBatches.CountAsync(b =>
            b.Status == BatchStatus.Completed || b.Status == BatchStatus.InventoryAdded);

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
        var finishedProducts = await _context.FinishedProducts
            .Include(fp => fp.Item)
            .ThenInclude(i => i.Inventories)
            .ToListAsync();

        var alerts = new List<LowStockAlertResponse>();

        foreach (var fp in finishedProducts)
        {
            if (fp.Item != null)
            {
                var currentStock = fp.Item.Inventories.Sum(i => i.CurrentStock);
                if (currentStock < fp.Item.MinStockLevel)
                {
                    alerts.Add(new LowStockAlertResponse
                    {
                        ItemId = fp.ItemId,
                        ItemName = fp.Item.ItemName,
                        CurrentStock = currentStock,
                        MinStockLevel = fp.Item.MinStockLevel
                    });
                }
            }
        }

        return alerts;
    }

    private ProductionBatchResponse MapToResponse(ProductionBatch batch, string recipeName, string productName)
    {
        return new ProductionBatchResponse
        {
            BatchId = batch.BatchId,
            RecipeId = batch.RecipeId,
            RecipeName = recipeName,
            ProductId = batch.ProductId,
            ProductName = productName,
            BatchMultiplier = batch.BatchMultiplier,
            EstimatedQuantity = batch.EstimatedQuantity,
            ActualQuantity = batch.ActualQuantity,
            ProductionDate = batch.ProductionDate,
            Stage = EnumDbValue.ToDbValue(batch.Stage),
            Status = EnumDbValue.ToDbValue(batch.Status),
            AssignedCook = batch.AssignedCook,
            QualityStatus = EnumDbValue.ToDbValue(batch.QualityStatus),
            RejectionReason = batch.RejectionReason,
            ImageUrl = batch.ImageUrl,
            Notes = batch.Notes
        };
    }
}
