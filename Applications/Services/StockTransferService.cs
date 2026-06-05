using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class StockTransferService : IStockTransferService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<StockTransferService> _logger;

    public StockTransferService(ScmDbContext context, ILogger<StockTransferService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<StockTransferResponse>>> GetAllTransfersAsync()
    {
        try
        {
            var transfers = await _context.StockTransfers
                .Include(st => st.Product)
                    .ThenInclude(p => p.Item)
                .Include(st => st.SourceLocation)
                .Include(st => st.DestLocation)
                .OrderByDescending(st => st.TransferDate)
                .Select(st => new StockTransferResponse
                {
                    TransferId = st.TransferId,
                    ProductId = st.ProductId,
                    ProductName = st.Product != null && st.Product.Item != null ? st.Product.Item.ItemName : "Unknown",
                    SourceLocationId = st.SourceLocationId,
                    SourceLocationName = st.SourceLocation != null ? st.SourceLocation.LocationName : "Unknown",
                    DestLocationId = st.DestLocationId,
                    DestLocationName = st.DestLocation != null ? st.DestLocation.LocationName : "Unknown",
                    TransferQuantity = st.TransferQuantity,
                    Status = st.Status,
                    TransferDate = st.TransferDate
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<StockTransferResponse>>.SuccessResponse(transfers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock transfers.");
            return ApiResponse<IEnumerable<StockTransferResponse>>.FailureResponse("An error occurred while fetching transfers.");
        }
    }

    public async Task<ApiResponse<StockTransferResponse>> CreateTransferAsync(CreateStockTransferRequest request, int userId)
    {
        try
        {
            if (request.TransferQuantity <= 0)
                return ApiResponse<StockTransferResponse>.FailureResponse("Transfer quantity must be greater than zero.");

            if (request.SourceLocationId == request.DestLocationId)
                return ApiResponse<StockTransferResponse>.FailureResponse("Source and destination locations cannot be the same.");

            var product = await _context.FinishedProducts
                .Include(p => p.Item)
                .FirstOrDefaultAsync(p => p.ProductId == request.ProductId);

            if (product == null || product.Item == null)
                return ApiResponse<StockTransferResponse>.FailureResponse("Product not found.");

            var sourceLocationExists = await _context.Locations.AnyAsync(l => l.LocationId == request.SourceLocationId);
            var destLocationExists = await _context.Locations.AnyAsync(l => l.LocationId == request.DestLocationId);

            if (!sourceLocationExists || !destLocationExists)
                return ApiResponse<StockTransferResponse>.FailureResponse("Source or destination location not found.");

            // Validate available stock
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.LocationId == request.SourceLocationId && i.ItemId == product.ItemId);

            if (inventory == null || inventory.CurrentStock < request.TransferQuantity)
            {
                return ApiResponse<StockTransferResponse>.FailureResponse($"Insufficient stock at source location. Available: {(inventory == null ? 0 : inventory.CurrentStock)}");
            }

            var transfer = new StockTransfer
            {
                ProductId = request.ProductId,
                SourceLocationId = request.SourceLocationId,
                DestLocationId = request.DestLocationId,
                TransferQuantity = request.TransferQuantity,
                Status = "Pending",
                TransferDate = DateTime.UtcNow
            };

            _context.StockTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            return ApiResponse<StockTransferResponse>.SuccessResponse(new StockTransferResponse
            {
                TransferId = transfer.TransferId,
                ProductId = transfer.ProductId,
                ProductName = product.Item.ItemName,
                SourceLocationId = transfer.SourceLocationId,
                DestLocationId = transfer.DestLocationId,
                TransferQuantity = transfer.TransferQuantity,
                Status = transfer.Status,
                TransferDate = transfer.TransferDate
            }, "Stock transfer created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock transfer.");
            return ApiResponse<StockTransferResponse>.FailureResponse("An error occurred while creating the transfer.");
        }
    }

    public async Task<ApiResponse<StockTransferResponse>> UpdateTransferStatusAsync(int transferId, UpdateStockTransferStatusRequest request, int userId)
    {
        try
        {
            var transfer = await _context.StockTransfers
                .Include(st => st.Product)
                    .ThenInclude(p => p.Item)
                .Include(st => st.SourceLocation)
                .Include(st => st.DestLocation)
                .FirstOrDefaultAsync(st => st.TransferId == transferId);

            if (transfer == null)
                return ApiResponse<StockTransferResponse>.FailureResponse("Transfer not found.");

            if (transfer.Status == "Completed" || transfer.Status == "Cancelled")
                return ApiResponse<StockTransferResponse>.FailureResponse($"Transfer is already {transfer.Status} and cannot be updated.");

            string newStatus = request.Status;
            
            // Valid transitions: Pending -> In Transit -> Completed. Also allows Pending/In Transit -> Cancelled.
            if (newStatus != "In Transit" && newStatus != "Completed" && newStatus != "Cancelled")
            {
                return ApiResponse<StockTransferResponse>.FailureResponse("Invalid status update.");
            }

            if (newStatus == "In Transit" && transfer.Status == "Pending")
            {
                // Deduct from source inventory
                var sourceInventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.LocationId == transfer.SourceLocationId && i.ItemId == transfer.Product.ItemId);

                if (sourceInventory == null || sourceInventory.CurrentStock < transfer.TransferQuantity)
                {
                    return ApiResponse<StockTransferResponse>.FailureResponse("Insufficient stock at source location to begin transit.");
                }

                sourceInventory.CurrentStock -= transfer.TransferQuantity;
                _context.Inventories.Update(sourceInventory);

                // Log deduction
                var log = new InventoryMovementLog
                {
                    ItemId = transfer.Product.ItemId,
                    LocationId = transfer.SourceLocationId,
                    ChangeQuantity = -transfer.TransferQuantity,
                    ActionType = "Transfer Out",
                    ReferenceId = transfer.TransferId.ToString(),
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                };
                _context.InventoryMovementLogs.Add(log);
            }
            else if (newStatus == "Completed" && transfer.Status == "In Transit")
            {
                // Add to destination inventory
                var destInventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.LocationId == transfer.DestLocationId && i.ItemId == transfer.Product.ItemId);

                if (destInventory == null)
                {
                    destInventory = new Inventory
                    {
                        LocationId = transfer.DestLocationId,
                        ItemId = transfer.Product.ItemId,
                        CurrentStock = transfer.TransferQuantity,
                        DriverId = 1 // Default/Placeholder if required, or update Inventory entity
                    };
                    _context.Inventories.Add(destInventory);
                }
                else
                {
                    destInventory.CurrentStock += transfer.TransferQuantity;
                    _context.Inventories.Update(destInventory);
                }

                // Log addition
                var log = new InventoryMovementLog
                {
                    ItemId = transfer.Product.ItemId,
                    LocationId = transfer.DestLocationId,
                    ChangeQuantity = transfer.TransferQuantity,
                    ActionType = "Transfer In",
                    ReferenceId = transfer.TransferId.ToString(),
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                };
                _context.InventoryMovementLogs.Add(log);
            }
            else if (newStatus == "Cancelled" && transfer.Status == "In Transit")
            {
                 // Revert stock back to source
                 var sourceInventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.LocationId == transfer.SourceLocationId && i.ItemId == transfer.Product.ItemId);
                 
                 if (sourceInventory != null)
                 {
                     sourceInventory.CurrentStock += transfer.TransferQuantity;
                     _context.Inventories.Update(sourceInventory);
                 }

                 var log = new InventoryMovementLog
                 {
                     ItemId = transfer.Product.ItemId,
                     LocationId = transfer.SourceLocationId,
                     ChangeQuantity = transfer.TransferQuantity,
                     ActionType = "Transfer Cancelled",
                     ReferenceId = transfer.TransferId.ToString(),
                     UserId = userId,
                     Timestamp = DateTime.UtcNow
                 };
                 _context.InventoryMovementLogs.Add(log);
            }

            transfer.Status = newStatus;
            _context.StockTransfers.Update(transfer);
            await _context.SaveChangesAsync();

            var response = new StockTransferResponse
            {
                TransferId = transfer.TransferId,
                ProductId = transfer.ProductId,
                ProductName = transfer.Product.Item.ItemName,
                SourceLocationId = transfer.SourceLocationId,
                SourceLocationName = transfer.SourceLocation?.LocationName ?? "Unknown",
                DestLocationId = transfer.DestLocationId,
                DestLocationName = transfer.DestLocation?.LocationName ?? "Unknown",
                TransferQuantity = transfer.TransferQuantity,
                Status = transfer.Status,
                TransferDate = transfer.TransferDate
            };

            return ApiResponse<StockTransferResponse>.SuccessResponse(response, $"Transfer status updated to {newStatus}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock transfer status.");
            return ApiResponse<StockTransferResponse>.FailureResponse("An error occurred while updating the transfer.");
        }
    }
}
