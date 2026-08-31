using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class StockTransferService : IStockTransferService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<StockTransferService> _logger;
    private readonly IStatusTransitionGuard _statusGuard;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;

    public StockTransferService(
        ScmDbContext context,
        ILogger<StockTransferService> logger,
        IStatusTransitionGuard statusGuard,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit)
    {
        _context = context;
        _logger = logger;
        _statusGuard = statusGuard;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ApiResponse<PagedData<StockTransferResponse>>> GetAllTransfersAsync(string? status = null, string? search = null, int page = 1, int pageSize = 10)
    {
        try
        {
            var query = _context.StockTransfers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (!EnumDbValue.TryParse<ShipmentStatus>(status, out var statusFilter))
                {
                    return ApiResponse<PagedData<StockTransferResponse>>.FailureResponse(
                        $"Invalid status filter '{status}'. Accepted values: {EnumDbValue.DescribeAccepted<ShipmentStatus>()}.");
                }

                query = query.Where(st => st.Status == statusFilter);
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

            // Materialise first, then map. The status enum has to be rendered through
            // EnumDbValue, which has no SQL translation, so the projection must run client side.
            var rows = await query
                .Include(st => st.Product)
                    .ThenInclude(p => p!.Item)
                .Include(st => st.SourceLocation)
                .Include(st => st.DestLocation)
                .OrderByDescending(st => st.TransferId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var transfers = rows.Select(MapToResponse).ToList();

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

    public async Task<ApiResponse<StockTransferResponse>> CreateTransferAsync(CreateStockTransferRequest request)
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
                Status = ShipmentStatus.Pending,
                TransferDate = request.TransferDate.HasValue ? DateTime.SpecifyKind(request.TransferDate.Value, DateTimeKind.Utc) : DateTime.UtcNow
            };

            _context.StockTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            _audit.Record(nameof(StockTransfer), transfer.TransferId.ToString(), "Created");
            await _context.SaveChangesAsync();

            return ApiResponse<StockTransferResponse>.SuccessResponse(new StockTransferResponse
            {
                TransferId = transfer.TransferId,
                ProductId = transfer.ProductId,
                ProductName = product.Item.ItemName,
                SourceLocationId = transfer.SourceLocationId,
                DestLocationId = transfer.DestLocationId,
                TransferQuantity = transfer.TransferQuantity,
                Status = EnumDbValue.ToDbValue(transfer.Status),
                TransferDate = transfer.TransferDate
            }, "Stock transfer created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock transfer.");
            return ApiResponse<StockTransferResponse>.FailureResponse("An error occurred while creating the transfer.");
        }
    }

    public async Task<ApiResponse<StockTransferResponse>> UpdateTransferAsync(int transferId, UpdateStockTransferRequest request)
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
            {
                return ApiResponse<StockTransferResponse>.FailureResponse("Transfer not found.");
            }

            if (transfer.Status != ShipmentStatus.Pending)
            {
                return ApiResponse<StockTransferResponse>.FailureResponse("Only Pending transfers can be edited.");
            }

            var product = await _context.FinishedProducts.Include(f => f.Item).FirstOrDefaultAsync(f => f.ProductId == request.ProductId);
            if (product == null)
            {
                return ApiResponse<StockTransferResponse>.FailureResponse("Invalid product selected.");
            }

            var destLocation = await _context.Locations.FindAsync(request.DestLocationId);
            if (destLocation == null)
            {
                return ApiResponse<StockTransferResponse>.FailureResponse("Invalid destination location selected.");
            }

            transfer.ProductId = request.ProductId;
            transfer.SourceLocationId = request.SourceLocationId;
            transfer.DestLocationId = request.DestLocationId;
            transfer.TransferQuantity = request.TransferQuantity;
            if (request.TransferDate.HasValue)
            {
                transfer.TransferDate = DateTime.SpecifyKind(request.TransferDate.Value, DateTimeKind.Utc);
            }

            await _context.SaveChangesAsync();

            _audit.Record(nameof(StockTransfer), transfer.TransferId.ToString(), "Updated");
            await _context.SaveChangesAsync();

            return ApiResponse<StockTransferResponse>.SuccessResponse(new StockTransferResponse
            {
                TransferId = transfer.TransferId,
                ProductId = transfer.ProductId,
                ProductName = product.Item.ItemName,
                SourceLocationId = transfer.SourceLocationId,
                SourceLocationName = transfer.SourceLocation?.LocationName,
                DestLocationId = transfer.DestLocationId,
                DestLocationName = destLocation.LocationName,
                TransferQuantity = transfer.TransferQuantity,
                Status = EnumDbValue.ToDbValue(transfer.Status),
                TransferDate = transfer.TransferDate
            }, "Stock transfer updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock transfer.");
            return ApiResponse<StockTransferResponse>.FailureResponse("An error occurred while updating the transfer.");
        }
    }

    public async Task<ApiResponse<StockTransferResponse>> UpdateTransferStatusAsync(int transferId, UpdateStockTransferStatusRequest request)
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

            if (_statusGuard.IsFinal(transfer.Status))
                return ApiResponse<StockTransferResponse>.FailureResponse(
                    $"Transfer is already {EnumDbValue.ToDbValue(transfer.Status)} and cannot be updated.");

            if (!EnumDbValue.TryParse<ShipmentStatus>(request.Status, out var newStatus)
                || newStatus == ShipmentStatus.Unspecified)
            {
                return ApiResponse<StockTransferResponse>.FailureResponse(
                    $"Invalid status update '{request.Status}'. Accepted values: {EnumDbValue.DescribeAccepted<ShipmentStatus>()}.");
            }

            // Previously the branches below were the only thing standing between a caller and an
            // unearned status: Pending -> Completed matched no branch, fell through, and marked the
            // transfer delivered without ever debiting the source. The guard closes that.
            _statusGuard.EnsureCanTransition(transfer.Status, newStatus);

            var actor = _currentUser.Current;

            // The stock movement, the audit entry and the status change are one posting.
            await _posting.ExecuteAsync(async () =>
            {
            if (newStatus == ShipmentStatus.InTransit && transfer.Status == ShipmentStatus.Pending)
            {
                // Deduct from source inventory
                var sourceInventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.LocationId == transfer.SourceLocationId && i.ItemId == transfer.Product.ItemId);

                if (sourceInventory == null || sourceInventory.CurrentStock < transfer.TransferQuantity)
                {
                    // Throwing rather than returning aborts the posting, so the audit entry and status
                    // change staged alongside it are rolled back too.
                    throw new InsufficientStockException(
                        "Insufficient stock at source location to begin transit. " +
                        $"Requested {transfer.TransferQuantity}, available {sourceInventory?.CurrentStock ?? 0}.");
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
                    UserId = actor.UserId,
                    UserName = actor.AuditName,
                    Timestamp = DateTime.UtcNow
                };
                _context.InventoryMovementLogs.Add(log);
            }
            else if (newStatus == ShipmentStatus.Completed && transfer.Status == ShipmentStatus.InTransit)
            {
                // The user explicitly requested that completed deliveries DO NOT add to the destination inventory.
                // They only want it to be a straight deduction from the Commissary.
                var log = new InventoryMovementLog
                {
                    ItemId = transfer.Product.ItemId,
                    LocationId = transfer.DestLocationId,
                    ChangeQuantity = 0,
                    ActionType = "Transfer Completed (No Addition)",
                    ReferenceId = transfer.TransferId.ToString(),
                    UserId = actor.UserId,
                    UserName = actor.AuditName,
                    Timestamp = DateTime.UtcNow
                };
                _context.InventoryMovementLogs.Add(log);
            }
            else if (newStatus == ShipmentStatus.Cancelled && transfer.Status == ShipmentStatus.InTransit)
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
                     UserId = actor.UserId,
                     UserName = actor.AuditName,
                     Timestamp = DateTime.UtcNow
                 };
                 _context.InventoryMovementLogs.Add(log);
            }

            _audit.Record(
                nameof(StockTransfer), transfer.TransferId.ToString(), "StatusUpdated",
                fieldName: nameof(transfer.Status),
                oldValue: EnumDbValue.ToDbValue(transfer.Status),
                newValue: EnumDbValue.ToDbValue(newStatus));

            transfer.Status = newStatus;
            _context.StockTransfers.Update(transfer);
            });

            var response = MapToResponse(transfer);

            return ApiResponse<StockTransferResponse>.SuccessResponse(
                response, $"Transfer status updated to {EnumDbValue.ToDbValue(newStatus)}");
        }
        catch (InvalidStatusTransitionException ex)
        {
            _logger.LogWarning("Rejected status transition on transfer {TransferId}: {Message}", transferId, ex.Message);
            return ApiResponse<StockTransferResponse>.FailureResponse(ex.Message);
        }
        catch (InsufficientStockException ex)
        {
            _logger.LogWarning("Transfer {TransferId} could not be dispatched: {Message}", transferId, ex.Message);
            return ApiResponse<StockTransferResponse>.FailureResponse(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The balance moved between being read and being written. Since Task 5 that is detected
            // rather than silently overwriting someone else's movement, so the honest answer is to ask
            // for a retry against fresh numbers.
            _logger.LogWarning(
                "Transfer {TransferId} hit a concurrent stock change and was not applied.", transferId);
            return ApiResponse<StockTransferResponse>.FailureResponse(
                "Stock at the source location changed while this transfer was being processed. " +
                "Reload and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock transfer status.");
            return ApiResponse<StockTransferResponse>.FailureResponse("An error occurred while updating the transfer.");
        }
    }

    private static StockTransferResponse MapToResponse(StockTransfer transfer) => new()
    {
        TransferId = transfer.TransferId,
        ProductId = transfer.ProductId,
        ProductName = transfer.Product?.Item?.ItemName ?? "Unknown",
        SourceLocationId = transfer.SourceLocationId,
        SourceLocationName = transfer.SourceLocation?.LocationName ?? "Unknown",
        DestLocationId = transfer.DestLocationId,
        DestLocationName = transfer.DestLocation?.LocationName ?? "Unknown",
        TransferQuantity = transfer.TransferQuantity,
        Status = EnumDbValue.ToDbValue(transfer.Status),
        TransferDate = transfer.TransferDate
    };

    public async Task<ApiResponse<TransferDashboardResponse>> GetTransferDashboardSummaryAsync()
    {
        try
        {
            var pendingCount = await _context.StockTransfers.CountAsync(st => st.Status == ShipmentStatus.Pending);
            var inTransitCount = await _context.StockTransfers.CountAsync(st => st.Status == ShipmentStatus.InTransit);
            var completedCount = await _context.StockTransfers.CountAsync(st => st.Status == ShipmentStatus.Completed);

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
