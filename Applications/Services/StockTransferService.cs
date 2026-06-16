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

    public async Task<ApiResponse<PagedData<StockTransferResponse>>> GetAllTransfersAsync(string? status = null, string? search = null, int page = 1, int pageSize = 10)
    {
        try
        {
            var query = _context.StockTransfers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(st => st.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(st => 
                    st.TransferId.ToString().Contains(lowerSearch) ||
                    (st.Product != null && st.Product.Item != null && st.Product.Item.ItemName.ToLower().Contains(lowerSearch)) ||
                    (st.SourceLocation != null && st.SourceLocation.LocationName.ToLower().Contains(lowerSearch)) ||
                    (st.DestLocation != null && st.DestLocation.LocationName.ToLower().Contains(lowerSearch))
                );
            }

            var totalCount = await query.CountAsync();
            var transfers = await query
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
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedData = new PagedData<StockTransferResponse>
            {
                Items = transfers,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<StockTransferResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock transfers.");
            return ApiResponse<PagedData<StockTransferResponse>>.FailureResponse("An error occurred while fetching transfers.");
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

            var auditLog = new AuditLog
            {
                EntityName = "StockTransfer",
                EntityId = transfer.TransferId.ToString(),
                Action = "Created",
                Timestamp = DateTime.UtcNow,
                UserId = userId
            };
            _context.AuditLogs.Add(auditLog);
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

            var auditLog = new AuditLog
            {
                EntityName = "StockTransfer",
                EntityId = transfer.TransferId.ToString(),
                FieldName = "Status",
                OldValue = transfer.Status,
                NewValue = newStatus,
                Action = "StatusUpdated",
                Timestamp = DateTime.UtcNow,
                UserId = userId
            };
            _context.AuditLogs.Add(auditLog);

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

    public async Task<ApiResponse<TransferDashboardResponse>> GetTransferDashboardSummaryAsync()
    {
        try
        {
            var pendingCount = await _context.StockTransfers.CountAsync(st => st.Status == "Pending");
            var inTransitCount = await _context.StockTransfers.CountAsync(st => st.Status == "In Transit");
            var completedCount = await _context.StockTransfers.CountAsync(st => st.Status == "Completed");

            var response = new TransferDashboardResponse
            {
                PendingCount = pendingCount,
                InTransitCount = inTransitCount,
                CompletedCount = completedCount
            };

            return ApiResponse<TransferDashboardResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transfer dashboard summary.");
            return ApiResponse<TransferDashboardResponse>.FailureResponse("An error occurred while fetching the dashboard summary.");
        }
    }

    public async Task<ApiResponse<PagedData<TransferHistoryResponse>>> GetTransferHistoryAsync(string? status = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 10)
    {
        try
        {
            var query = _context.AuditLogs.Where(a => a.EntityName == "StockTransfer").AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            var totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = logs.Select(l => new TransferHistoryResponse
            {
                LogId = l.LogId,
                TransferId = l.EntityId,
                Action = l.Action,
                FieldName = l.FieldName,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                Timestamp = l.Timestamp,
                UserId = l.UserId
            });

            var pagedData = new PagedData<TransferHistoryResponse>
            {
                Items = result,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<TransferHistoryResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transfer history.");
            return ApiResponse<PagedData<TransferHistoryResponse>>.FailureResponse("An error occurred while fetching transfer history.");
        }
    }
}
