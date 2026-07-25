using Api.Contracts.Production;
using Applications.Interfaces;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace Applications.Services;

public class ProductionService : IProductionService
{
    private readonly ScmDbContext _context;

    public ProductionService(ScmDbContext context)
    {
        _context = context;
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
            Stage = "Preparation",
            Status = "Scheduled",
            AssignedCook = request.AssignedCook,
            QualityStatus = "Pending"
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

        bool isStarting = batch.Status == "Scheduled" && request.Stage != "QA Review" && request.Stage != "Completed" && request.Stage != "Cancelled";

        batch.Stage = request.Stage;
        if (request.Stage == "Completed")
        {
            batch.Status = "Completed";
        }
        else if (request.Stage == "Cancelled")
        {
            batch.Status = "Cancelled";
        }
        else if (request.Stage != "QA Review")
        {
            batch.Status = "In Progress";
        }

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
                    var requiredQuantity = ingredient.StandardQuantity * batch.BatchMultiplier;
                    var inventories = await _context.Inventories
                        .Where(i => i.ItemId == ingredient.ItemId && i.CurrentStock > 0)
                        .OrderBy(i => i.LocationId)
                        .ToListAsync();

                    int remainingToDeduct = requiredQuantity;
                    foreach (var inv in inventories)
                    {
                        if (remainingToDeduct <= 0) break;

                        int deductAmount = Math.Min(inv.CurrentStock, remainingToDeduct);
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
                        var item = await _context.Items.FindAsync(ingredient.ItemId);
                        throw new Exception($"Insufficient stock for ingredient {item?.ItemName ?? ingredient.ItemId.ToString()}. Required: {requiredQuantity}, Short by: {remainingToDeduct}");
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
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

        if (request.IsApproved)
        {
            batch.QualityStatus = "Approved";
            batch.Status = "Passed QA";
            batch.Stage = "Packaging";
        }
        else
        {
            batch.QualityStatus = "Rejected";
            batch.Status = "Rejected";
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
        if (batch.Status == "Inventory Added") throw new Exception("Batch already added to inventory.");

        // Determine actual quantity produced - assuming estimated for now if not set
        int actualQty = batch.ActualQuantity > 0 ? batch.ActualQuantity : batch.EstimatedQuantity;

        // Add finished goods to inventory. Fetch the "Finished Goods" location.
        var finishedGoodsLocation = await _context.Locations
            .FirstOrDefaultAsync(l => l.LocationName == "Finished Goods");
            
        var locationId = finishedGoodsLocation?.LocationId ?? 1; // Fallback to 1
        
        // Determine actual item ID from the finished product
        var actualItemId = batch.Recipe?.Product?.ItemId ?? throw new Exception("Item ID not found for this product.");

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

        batch.Status = "Inventory Added";

        await _context.SaveChangesAsync();
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
        var active = await _context.ProductionBatches.CountAsync(b => b.Status == "In Progress" || b.Status == "Scheduled");
        var delayed = await _context.ProductionBatches.CountAsync(b => b.ProductionDate < DateTime.UtcNow && b.Status != "Completed" && b.Status != "Rejected" && b.Status != "Inventory Added");
        var passedQa = await _context.ProductionBatches.CountAsync(b => b.Status == "Passed QA");
        var completed = await _context.ProductionBatches.CountAsync(b => b.Status == "Completed" || b.Status == "Inventory Added");

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
            Stage = batch.Stage,
            Status = batch.Status,
            AssignedCook = batch.AssignedCook,
            QualityStatus = batch.QualityStatus,
            RejectionReason = batch.RejectionReason,
            ImageUrl = batch.ImageUrl,
            Notes = batch.Notes
        };
    }
}
