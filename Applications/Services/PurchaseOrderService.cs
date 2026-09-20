using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<PurchaseOrderService> _logger;
    private readonly IStatusTransitionGuard _statusGuard;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILocationResolver _locations;

    public PurchaseOrderService(
        ScmDbContext context,
        ILogger<PurchaseOrderService> logger,
        IStatusTransitionGuard statusGuard,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILocationResolver locations)
    {
        _context = context;
        _logger = logger;
        _statusGuard = statusGuard;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _locations = locations;
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Creating purchase order for supplier ID {SupplierId}", request.SupplierId);

            // Validate Supplier existence
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
            if (supplier == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Supplier with ID {request.SupplierId} not found.");
            }

            // Validate Expected Arrival Date (must be in future)
            if (request.ExpectedArrivalDate <= DateTime.UtcNow)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse("Expected arrival date must be in the future.");
            }

            // Validate Item quantities and existence
            if (request.Items == null || !request.Items.Any())
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse("Purchase order must contain at least one item.");
            }

            var poItems = new List<PurchaseOrderItem>();
            foreach (var itemReq in request.Items)
            {
                if (itemReq.PoItemQuantity <= 0)
                {
                    return ApiResponse<PurchaseOrderResponse>.FailureResponse("Quantity must be greater than zero.");
                }

                // Enforce whole-number quantities
                if (itemReq.PoItemQuantity != Math.Floor(itemReq.PoItemQuantity))
                {
                    return ApiResponse<PurchaseOrderResponse>.FailureResponse("Quantity must be a whole number (no decimals).");
                }

                if (itemReq.TotalPrice < 0)
                {
                    return ApiResponse<PurchaseOrderResponse>.FailureResponse("Total price cannot be negative.");
                }

                var item = await _context.Items.FindAsync(itemReq.ItemId);
                if (item == null)
                {
                    return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Item with ID {itemReq.ItemId} not found.");
                }

                var purchaseUomId = itemReq.PurchaseUomId ?? 0;
                if (purchaseUomId <= 0)
                {
                    var catalogEntry = await _context.SupplierItems
                        .FirstOrDefaultAsync(si => si.SupplierId == request.SupplierId && si.ItemId == itemReq.ItemId);
                    if (catalogEntry != null)
                        purchaseUomId = catalogEntry.PurchaseUomId;
                }

                if (purchaseUomId <= 0)
                    purchaseUomId = item.StockUomId;

                poItems.Add(new PurchaseOrderItem
                {
                    ItemId = itemReq.ItemId,
                    SupplierId = request.SupplierId,
                    PoItemQuantity = itemReq.PoItemQuantity,
                    TotalPrice = itemReq.TotalPrice,
                    PurchaseUomId = purchaseUomId,
                    ReceivedQuantity = 0
                });
            }

            var calculatedTotal = poItems.Sum(p => p.TotalPrice);
            var finalTotal = calculatedTotal > 0 ? calculatedTotal : request.TotalAmount;

            var orderDate = DateTime.UtcNow;

            // Number and insert together, so a failed insert releases the number instead of leaving a
            // hole in the sequence.
            var order = await _posting.ExecuteAsync(async () =>
            {
                // Determine initial status: caller may specify "Draft" (save) or "Pending Approval" (submit)
                var initialStatus = PurchaseOrderStatus.Draft;
                if (!string.IsNullOrWhiteSpace(request.InitialStatus)
                    && EnumDbValue.TryParse<PurchaseOrderStatus>(request.InitialStatus, out var parsedInitial)
                    && parsedInitial != PurchaseOrderStatus.Unspecified)
                {
                    initialStatus = parsedInitial;
                }

                var newOrder = new PurchaseOrder
                {
                    PoNumber = await _documentNumbers.NextAsync(DocumentType.PurchaseOrder, orderDate),
                    PrId = request.PrId,
                    SupplierId = request.SupplierId,
                    OrderDate = orderDate,
                    ExpectedArrivalDate = request.ExpectedArrivalDate,
                    Status = initialStatus,
                    PaymentType = request.PaymentType,
                    ProofImageUrl = string.Empty,
                    TotalAmount = finalTotal,
                    RequestedBy = request.RequestedBy ?? string.Empty,
                    PurchaseOrderItems = poItems
                };

                _context.PurchaseOrders.Add(newOrder);
                return newOrder;
            });

            // Reload relationships to return details
            var reloadedOrder = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseRequisition)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                        .ThenInclude(i => i!.StockUom)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.PurchaseUom)
                .FirstOrDefaultAsync(o => o.PoId == order.PoId);

            var response = MapToResponse(reloadedOrder!);
            _logger.LogInformation("Purchase order created successfully with ID {PoId}", order.PoId);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Purchase order created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating purchase order: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> GetPurchaseOrderByIdAsync(int id)
    {
        try
        {
            var order = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseRequisition)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                        .ThenInclude(i => i!.StockUom)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.PurchaseUom)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");

            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(MapToResponse(order));
        }
        catch (Exception ex)
        {
            _logger.LogError("Error retrieving purchase order {PoId}: {Message}", id, ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedData<PurchaseOrderResponse>>> GetPurchaseOrdersAsync(string? status = null, string? search = null, int page = 1, int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("Retrieving purchase orders");

            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                        .ThenInclude(i => i!.StockUom)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.PurchaseUom)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (!EnumDbValue.TryParse<PurchaseOrderStatus>(status, out var statusFilter))
                {
                    return ApiResponse<PagedData<PurchaseOrderResponse>>.FailureResponse(
                        $"Invalid status filter '{status}'. Accepted values: {EnumDbValue.DescribeAccepted<PurchaseOrderStatus>()}.");
                }

                query = query.Where(o => o.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(o => 
                    o.PoId.ToString().Contains(lowerSearch) || 
                    (!string.IsNullOrEmpty(o.PoNumber) && o.PoNumber.ToLower().Contains(lowerSearch)) ||
                    (o.Supplier != null && o.Supplier.CompanyName.ToLower().Contains(lowerSearch)) ||
                    o.PurchaseOrderItems.Any(poi => poi.Item != null && poi.Item.ItemName.ToLower().Contains(lowerSearch))
                );
            }

            var totalCount = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.PoId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var responses = orders.Select(MapToResponse);

            var pagedData = new PagedData<PurchaseOrderResponse>
            {
                Items = responses,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<PurchaseOrderResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error retrieving purchase orders: {Message}", ex.Message);
            return ApiResponse<PagedData<PurchaseOrderResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> UpdateOrderStatusAsync(int id, UpdatePurchaseOrderQaRequest request)
    {
        try
        {
            _logger.LogInformation("Updating purchase order ID {PoId} status to {Status}", id, request.Status);

            if (!EnumDbValue.TryParse<PurchaseOrderStatus>(request.Status, out var requestedStatus)
                || requestedStatus == PurchaseOrderStatus.Unspecified)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse(
                    $"Invalid status: {request.Status}. Allowed statuses are: {EnumDbValue.DescribeAccepted<PurchaseOrderStatus>()}.");
            }

            var order = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");
            }

            var oldStatus = order.Status;
            var actor = _currentUser.Current;

            // The lifecycle is Pending -> Arrived -> Completed, because QA inspection happens on
            // arrival. Jumping straight from Pending to Completed would post stock that nobody
            // inspected, so the guard refuses it.
            _statusGuard.EnsureCanTransition(oldStatus, requestedStatus);

            order.Status = requestedStatus;

            // Save AdminNotes for approval-workflow transitions (Rejected, Returned)
            if (!string.IsNullOrWhiteSpace(request.AdminNotes))
            {
                order.AdminNotes = request.AdminNotes;
            }

            // Handle QA specific fields if it's a QA transition
            if (requestedStatus is PurchaseOrderStatus.Completed or PurchaseOrderStatus.Rejected)
            {
                if (!string.IsNullOrEmpty(request.QaNotes) || !string.IsNullOrEmpty(request.QaStatus))
                {
                    order.QaNotes = request.QaNotes;
                    order.QaStatus = request.QaStatus;
                    order.QaInspectedDate = DateTime.UtcNow;
                    order.InspectedBy = request.InspectedBy ?? actor.AuditName;
                }
            }

            // Trigger stock additions & transaction logging ONLY on "Completed" (passed QA)
            var isCompletedTransition = requestedStatus == PurchaseOrderStatus.Completed
                && oldStatus != PurchaseOrderStatus.Completed;

            // Everything from here on is one posting: the status change, the received quantities, the
            // balances and the ledger entries either all land or none do.
            await _posting.ExecuteAsync(async () =>
            {
            if (isCompletedTransition)
            {
                // A location chosen for its role, validated, and never invented. The old code took
                // whichever row Locations.FirstOrDefaultAsync() happened to return and created a
                // "Storage" location if the table was empty, so stock landed wherever by accident.
                var location = await _locations.ResolveReceivingLocationAsync(request.ReceivingLocationId);

                foreach (var poItem in order.PurchaseOrderItems)
                {
                    poItem.ReceivedQuantity = poItem.PoItemQuantity;

                    // Update or Add Inventory
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(inv => inv.ItemId == poItem.ItemId && inv.LocationId == location.LocationId);

                    if (inventory != null)
                    {
                        inventory.CurrentStock += poItem.PoItemQuantity;
                        _context.Inventories.Update(inventory);
                    }
                    else
                    {
                        inventory = new Inventory
                        {
                            ItemId = poItem.ItemId,
                            LocationId = location.LocationId,
                            // No driver. A driver belongs to a shipment, not to a stock balance; the old
                            // code fabricated a "Default Driver" purely to fill this field.
                            DriverId = null,
                            CurrentStock = poItem.PoItemQuantity
                        };
                        _context.Inventories.Add(inventory);
                    }

                    // Log Inventory Movement Transaction
                    var movementLog = new InventoryMovementLog
                    {
                        ItemId = poItem.ItemId,
                        LocationId = location.LocationId,
                        ChangeQuantity = poItem.PoItemQuantity,
                        ActionType = "Order Arrival",
                        ReferenceId = order.PoNumber,
                        UserId = actor.UserId,
                        UserName = actor.AuditName,
                        Timestamp = DateTime.UtcNow
                    };
                    _context.InventoryMovementLogs.Add(movementLog);
                }
            }

                _context.PurchaseOrders.Update(order);

                _audit.Record(
                    nameof(PurchaseOrder), order.PoNumber, "StatusUpdated",
                    fieldName: nameof(order.Status),
                    oldValue: EnumDbValue.ToDbValue(oldStatus),
                    newValue: EnumDbValue.ToDbValue(requestedStatus));
            });

            var response = MapToResponse(order);
            _logger.LogInformation("Purchase order ID {PoId} updated successfully", id);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Status updated successfully");
        }
        catch (InvalidStatusTransitionException ex)
        {
            // A rejected lifecycle jump is a caller mistake, not a server fault: surface the
            // explanation verbatim rather than burying it in "An error occurred".
            _logger.LogWarning("Rejected status transition on purchase order {PoId}: {Message}", id, ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating purchase order status: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> UpdatePurchaseOrderAsync(int id, CreatePurchaseOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Updating purchase order ID {PoId}", id);

            var order = await _context.PurchaseOrders
                .Include(o => o.PurchaseOrderItems)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");
            }

            // Only Draft or Returned POs may be edited in the new approval workflow.
            // Legacy Pending is also allowed for backward compatibility.
            if (order.Status != PurchaseOrderStatus.Draft
                && order.Status != PurchaseOrderStatus.Returned
                && order.Status != PurchaseOrderStatus.Pending)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse(
                    $"A {EnumDbValue.ToDbValue(order.Status)} purchase order cannot be edited.");
            }

            // Validate Supplier existence
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
            if (supplier == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Supplier with ID {request.SupplierId} not found.");
            }

            order.SupplierId = request.SupplierId;
            order.ExpectedArrivalDate = request.ExpectedArrivalDate;
            order.PaymentType = request.PaymentType;

            // Update items
            if (request.Items != null && request.Items.Any())
            {
                _context.PurchaseOrderItems.RemoveRange(order.PurchaseOrderItems);
                
                var poItems = new List<PurchaseOrderItem>();
                foreach (var itemReq in request.Items)
                {
                    var item = await _context.Items.FindAsync(itemReq.ItemId);
                    var purchaseUomId = itemReq.PurchaseUomId ?? 0;

                    if (purchaseUomId <= 0)
                    {
                        var catalogEntry = await _context.SupplierItems
                            .FirstOrDefaultAsync(si => si.SupplierId == request.SupplierId && si.ItemId == itemReq.ItemId);
                        if (catalogEntry != null)
                            purchaseUomId = catalogEntry.PurchaseUomId;
                    }

                    if (purchaseUomId <= 0 && item != null)
                        purchaseUomId = item.StockUomId;

                    poItems.Add(new PurchaseOrderItem
                    {
                        ItemId = itemReq.ItemId,
                        SupplierId = request.SupplierId,
                        PoItemQuantity = itemReq.PoItemQuantity,
                        TotalPrice = itemReq.TotalPrice,
                        PurchaseUomId = purchaseUomId,
                        ReceivedQuantity = 0
                    });
                }
                order.PurchaseOrderItems = poItems;
                order.TotalAmount = poItems.Sum(p => p.TotalPrice);
            }

            _context.PurchaseOrders.Update(order);
            await _context.SaveChangesAsync();

            // Reload details
            var reloadedOrder = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                        .ThenInclude(i => i!.StockUom)
                .Include(o => o.PurchaseOrderItems)
                    .ThenInclude(poi => poi.PurchaseUom)
                .FirstOrDefaultAsync(o => o.PoId == order.PoId);

            var response = MapToResponse(reloadedOrder!);
            _logger.LogInformation("Purchase order ID {PoId} updated successfully", id);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Purchase order updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating purchase order: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> UploadReceiptAsync(int id, IFormFile file)
    {
        try
        {
            _logger.LogInformation("Uploading receipt for purchase order ID {PoId}", id);

            var order = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");
            }

            if (file == null || file.Length == 0)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse("No file was uploaded.");
            }

            // Convert IFormFile to Base64 string
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                string base64String = Convert.ToBase64String(fileBytes);
                // Create a data URI scheme string (e.g. data:image/jpeg;base64,....)
                order.ProofImageUrl = $"data:{file.ContentType};base64,{base64String}";
            }

            _context.PurchaseOrders.Update(order);
            await _context.SaveChangesAsync();

            var response = MapToResponse(order);
            _logger.LogInformation("Receipt uploaded successfully for purchase order ID {PoId}", id);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Receipt attachment uploaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error uploading receipt: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedData<TransactionHistoryResponse>>> GetTransactionHistoryAsync(string? filterType = null, DateTime? specificDate = null, int page = 1, int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("Fetching transaction history");

            var query = _context.InventoryMovementLogs
                .Include(l => l.Item)
                .Include(l => l.Location)
                .AsQueryable();

            query = ApplyFilter(query, filterType, specificDate);

            var totalCount = await query.CountAsync();
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var responses = logs.Select(MapToTransactionResponse);

            var pagedData = new PagedData<TransactionHistoryResponse>
            {
                Items = responses,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<TransactionHistoryResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error retrieving transaction history: {Message}", ex.Message);
            return ApiResponse<PagedData<TransactionHistoryResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<string> ExportTransactionHistoryCsvAsync(string? filterType = null, DateTime? specificDate = null)
    {
        try
        {
            _logger.LogInformation("Exporting transaction history to CSV");

            var query = _context.InventoryMovementLogs
                .Include(l => l.Item)
                .Include(l => l.Location)
                .AsQueryable();

            query = ApplyFilter(query, filterType, specificDate);

            var logs = await query.OrderByDescending(l => l.Timestamp).Take(5000).ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("MovementId,ItemId,ItemName,LocationName,ChangeQuantity,ActionType,ReferenceId,UserId,Timestamp");

            foreach (var log in logs)
            {
                var itemName = EscapeCsv(log.Item?.ItemName ?? string.Empty);
                var locationName = EscapeCsv(log.Location?.LocationName ?? string.Empty);
                var actionType = EscapeCsv(log.ActionType);
                var referenceId = EscapeCsv(log.ReferenceId);

                builder.AppendLine($"{log.MovementId},{log.ItemId},{itemName},{locationName},{log.ChangeQuantity},{actionType},{referenceId},{log.UserId},{log.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error exporting transaction history to CSV: {Message}", ex.Message);
            throw;
        }
    }

    private static IQueryable<InventoryMovementLog> ApplyFilter(IQueryable<InventoryMovementLog> query, string? filterType, DateTime? specificDate)
    {
        if (string.IsNullOrWhiteSpace(filterType))
        {
            return query;
        }

        switch (filterType.ToLower())
        {
            case "day":
                var dayLimit = DateTime.UtcNow.Date;
                query = query.Where(l => l.Timestamp >= dayLimit);
                break;
            case "week":
                var weekLimit = DateTime.UtcNow.Date.AddDays(-7);
                query = query.Where(l => l.Timestamp >= weekLimit);
                break;
            case "month":
                var monthLimit = DateTime.UtcNow.Date.AddDays(-30);
                query = query.Where(l => l.Timestamp >= monthLimit);
                break;
            case "year":
                var yearLimit = DateTime.UtcNow.Date.AddDays(-365);
                query = query.Where(l => l.Timestamp >= yearLimit);
                break;
            case "specificdate":
                if (specificDate.HasValue)
                {
                    var specDate = specificDate.Value.Date;
                    query = query.Where(l => l.Timestamp.Date == specDate);
                }
                break;
        }

        return query;
    }

    private static PurchaseOrderResponse MapToResponse(PurchaseOrder order)
    {
        return new PurchaseOrderResponse
        {
            PoId = order.PoId,
            PrId = order.PrId,
            PrNumber = order.PurchaseRequisition?.PrNumber,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.CompanyName ?? string.Empty,
            OrderDate = order.OrderDate,
            PoNumber = order.PoNumber,
            ExpectedArrivalDate = order.ExpectedArrivalDate,
            Status = EnumDbValue.ToDbValue(order.Status),
            PaymentType = order.PaymentType,
            ProofImageUrl = order.ProofImageUrl,
            TotalAmount = order.TotalAmount,
            RequestedBy = order.RequestedBy,
            AdminNotes = order.AdminNotes,
            QaNotes = order.QaNotes,
            QaInspectedDate = order.QaInspectedDate,
            QaStatus = order.QaStatus,
            InspectedBy = order.InspectedBy,
            Items = order.PurchaseOrderItems.Select(poi => new PurchaseOrderItemResponse
            {
                PoItemId = poi.PoItemId,
                ItemId = poi.ItemId,
                ItemName = poi.Item?.ItemName ?? string.Empty,
                PoItemQuantity = poi.PoItemQuantity,
                ReceivedQuantity = poi.ReceivedQuantity,
                TotalPrice = poi.TotalPrice,
                PurchaseUomId = poi.PurchaseUomId,
                PurchaseUomName = poi.PurchaseUom?.Abbreviation ?? poi.Item?.StockUom?.Abbreviation ?? "Unit"
            }).ToList()
        };
    }

    public async Task<ApiResponse<List<PrItemOrderedQtyResponse>>> GetOrderedQtyForPrAsync(int prId)
    {
        try
        {
            // Sum quantities from all non-cancelled POs linked to this PR
            var ordered = await _context.PurchaseOrders
                .Where(po => po.PrId == prId
                    && po.Status != PurchaseOrderStatus.Cancelled
                    && po.Status != PurchaseOrderStatus.Rejected)
                .SelectMany(po => po.PurchaseOrderItems)
                .GroupBy(poi => poi.ItemId)
                .Select(g => new PrItemOrderedQtyResponse
                {
                    ItemId = g.Key,
                    OrderedQty = g.Sum(poi => poi.PoItemQuantity)
                })
                .ToListAsync();

            return ApiResponse<List<PrItemOrderedQtyResponse>>.SuccessResponse(ordered);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error fetching ordered qty for PR {PrId}: {Message}", prId, ex.Message);
            return ApiResponse<List<PrItemOrderedQtyResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> GetNextPoNumberPreviewAsync()
    {
        try
        {
            var year = DateTime.UtcNow.Year;
            var docType = EnumDbValue.ToDbValue(DocumentType.PurchaseOrder);

            var currentSeq = await _context.DocumentSequences
                .Where(s => s.DocType == docType && s.Year == year)
                .Select(s => (int?)s.LastNumber)
                .FirstOrDefaultAsync() ?? 0;

            var nextSeq = currentSeq + 1;
            var preview = DocumentNumbering.Format(DocumentType.PurchaseOrder, year, nextSeq);
            return ApiResponse<string>.SuccessResponse(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error previewing next PO number: {Message}", ex.Message);
            return ApiResponse<string>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    private static TransactionHistoryResponse MapToTransactionResponse(InventoryMovementLog log)
    {
        return new TransactionHistoryResponse
        {
            MovementId = log.MovementId,
            ItemId = log.ItemId,
            ItemName = log.Item?.ItemName ?? string.Empty,
            LocationName = log.Location?.LocationName ?? string.Empty,
            ChangeQuantity = log.ChangeQuantity,
            ActionType = log.ActionType,
            ReferenceId = log.ReferenceId,
            UserId = log.UserId,
            Timestamp = log.Timestamp
        };
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> DeleteReceiptAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting receipt for purchase order ID {PoId}", id);

            var order = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");
            }

            order.ProofImageUrl = string.Empty;
            _context.PurchaseOrders.Update(order);
            await _context.SaveChangesAsync();

            var response = MapToResponse(order);
            _logger.LogInformation("Receipt deleted successfully for purchase order ID {PoId}", id);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Receipt attachment deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error deleting receipt: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    private static string EscapeCsv(string field)
    {
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }
}
