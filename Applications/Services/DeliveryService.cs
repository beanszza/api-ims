using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class DeliveryService : IDeliveryService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly ILocationResolver _locations;
    private readonly ICurrentUserService _currentUser;
    private readonly IStatusTransitionGuard _statusGuard;
    private readonly IAuditTrail _audit;
    private readonly IPostingTransaction _posting;
    private readonly ILogger<DeliveryService> _logger;

    public DeliveryService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        ILocationResolver locations,
        ICurrentUserService currentUser,
        IStatusTransitionGuard statusGuard,
        IAuditTrail audit,
        IPostingTransaction posting,
        ILogger<DeliveryService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _locations = locations;
        _currentUser = currentUser;
        _statusGuard = statusGuard;
        _audit = audit;
        _posting = posting;
        _logger = logger;
    }

    public async Task<ApiResponse<DeliveryResponse>> CreateDeliveryAsync(CreateDeliveryRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
            {
                return ApiResponse<DeliveryResponse>.FailureResponse("A delivery must contain at least one item.");
            }

            var po = await _context.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                .Include(p => p.PurchaseOrderItems)
                    .ThenInclude(poi => poi.PurchaseUom)
                .FirstOrDefaultAsync(p => p.PoId == request.PoId, ct);

            if (po == null)
            {
                return ApiResponse<DeliveryResponse>.FailureResponse($"Purchase order {request.PoId} was not found.");
            }

            if (po.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Rejected or PurchaseOrderStatus.Draft or PurchaseOrderStatus.PendingApproval)
            {
                return ApiResponse<DeliveryResponse>.FailureResponse($"Cannot create a delivery for a purchase order in '{po.Status}' status.");
            }

            // All supplier deliveries are inbound to the single Commissary receiving point.
            // A storage location is selected later, during Put Away.
            var receivingLocation = await _locations.ResolveReceivingLocationAsync(null);
            var scheduledDate = request.ScheduledDate ?? DateTime.UtcNow;
            var deliveryNumber = await _documentNumbers.NextAsync(DocumentType.Delivery, scheduledDate);
            var actor = _currentUser.Current;

            var delivery = new Delivery
            {
                DeliveryNumber = deliveryNumber,
                PoId = po.PoId,
                SupplierId = po.SupplierId,
                ReceivingLocationId = receivingLocation.LocationId,
                Status = DeliveryStatus.Scheduled,
                ScheduledDate = scheduledDate,
                ExpectedArrivalDate = request.ExpectedArrivalDate ?? po.ExpectedArrivalDate,
                TrackingNumber = request.TrackingNumber?.Trim(),
                Carrier = request.Carrier?.Trim(),
                DriverName = request.DriverName?.Trim(),
                VehiclePlateNumber = request.VehiclePlateNumber?.Trim(),
                DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim(),
                PaymentType = request.PaymentType ?? po.PaymentType,
                Notes = request.Notes?.Trim(),
                AttachmentUrl = request.AttachmentUrl?.Trim(),
                ScheduledAttachment = request.ScheduledAttachmentBase64?.Trim() ?? request.AttachmentUrl?.Trim(),
                CreatedBy = actor.AuditName,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var itemReq in request.Items)
            {
                var poItem = po.PurchaseOrderItems.FirstOrDefault(i => i.PoItemId == itemReq.PoItemId);
                if (poItem == null)
                {
                    return ApiResponse<DeliveryResponse>.FailureResponse($"PO item {itemReq.PoItemId} does not belong to purchase order {po.PoNumber}.");
                }

                if (itemReq.DeclaredQuantity <= 0)
                {
                    return ApiResponse<DeliveryResponse>.FailureResponse($"Shipment quantity for item '{poItem.Item?.ItemName ?? poItem.ItemId.ToString()}' must be greater than zero.");
                }

                // In-flight scheduled, in-transit, or arrived shipments pending GRN
                var inFlightQuantity = await _context.DeliveryItems
                    .Where(di => di.PoItemId == poItem.PoItemId &&
                                 (di.Delivery.Status == DeliveryStatus.Scheduled || di.Delivery.Status == DeliveryStatus.InTransit || di.Delivery.Status == DeliveryStatus.Arrived))
                    .SumAsync(di => (decimal?)di.DeclaredQuantity, ct) ?? 0m;

                var remainingQuantity = poItem.PoItemQuantity - poItem.ReceivedQuantity - inFlightQuantity;
                if (itemReq.DeclaredQuantity > remainingQuantity)
                {
                    return ApiResponse<DeliveryResponse>.FailureResponse(
                        $"Declared quantity ({itemReq.DeclaredQuantity}) exceeds remaining outstanding quantity ({remainingQuantity}) for item '{poItem.Item?.ItemName ?? poItem.ItemId.ToString()}'.");
                }

                delivery.Items.Add(new DeliveryItem
                {
                    PoItemId = poItem.PoItemId,
                    ItemId = poItem.ItemId,
                    DeclaredQuantity = itemReq.DeclaredQuantity,
                    PurchaseUomId = poItem.PurchaseUomId
                });
            }

            await _posting.ExecuteAsync(async () =>
            {
                _context.Deliveries.Add(delivery);

                // If PO was in Approved status, transition to Ordered upon scheduling delivery
                if (po.Status == PurchaseOrderStatus.Approved)
                {
                    var oldPoStatus = po.Status;
                    po.Status = PurchaseOrderStatus.Ordered;
                    _context.PurchaseOrders.Update(po);

                    _audit.Record(
                        nameof(PurchaseOrder),
                        po.PoNumber,
                        "StatusUpdated",
                        fieldName: nameof(po.Status),
                        oldValue: EnumDbValue.ToDbValue(oldPoStatus),
                        newValue: EnumDbValue.ToDbValue(PurchaseOrderStatus.Ordered));
                }

                await _context.SaveChangesAsync(ct);

                _audit.Record(
                    nameof(Delivery),
                    delivery.DeliveryId.ToString(),
                    "Created",
                    null,
                    null,
                    $"Created delivery {delivery.DeliveryNumber} for PO {po.PoNumber}");
            });

            return await GetDeliveryByIdAsync(delivery.DeliveryId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating delivery for PO {PoId}", request.PoId);
            return ApiResponse<DeliveryResponse>.FailureResponse($"Failed to create delivery: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PagedData<DeliveryResponse>>> GetDeliveriesAsync(
        int? poId = null,
        string? status = null,
        int page = 1,
        int pageSize = 50,
        bool? eligibleForGrn = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.Deliveries
                .Include(d => d.PurchaseOrder)
                .Include(d => d.Supplier)
                .Include(d => d.ReceivingLocation)
                .Include(d => d.GoodsReceipts)
                .Include(d => d.Items)
                    .ThenInclude(i => i.Item)
                .Include(d => d.Items)
                    .ThenInclude(i => i.PurchaseUom)
                .Include(d => d.Items)
                    .ThenInclude(i => i.PurchaseOrderItem)
                .AsQueryable();

            if (eligibleForGrn == true)
            {
                query = query.Where(d => d.Status == DeliveryStatus.Arrived && !d.GoodsReceipts.Any(gr => gr.Status != GoodsReceiptStatus.Cancelled));
            }
            else if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<DeliveryStatus>(status, out var parsedStatus))
                {
                    query = query.Where(d => d.Status == parsedStatus);
                }
            }

            if (poId.HasValue && poId.Value > 0)
            {
                query = query.Where(d => d.PoId == poId.Value);
            }

            var totalItems = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(d => d.DeliveryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var responses = items.Select(MapToResponse).ToList();
            var pagedData = new PagedData<DeliveryResponse>
            {
                Items = responses,
                TotalCount = totalItems,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<DeliveryResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching deliveries");
            return ApiResponse<PagedData<DeliveryResponse>>.FailureResponse("An error occurred while fetching deliveries.");
        }
    }

    public async Task<ApiResponse<DeliveryResponse>> GetDeliveryByIdAsync(int deliveryId, CancellationToken ct = default)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                .Include(d => d.Supplier)
                .Include(d => d.ReceivingLocation)
                .Include(d => d.GoodsReceipts)
                .Include(d => d.Items)
                    .ThenInclude(i => i.Item)
                .Include(d => d.Items)
                    .ThenInclude(i => i.PurchaseUom)
                .Include(d => d.Items)
                    .ThenInclude(i => i.PurchaseOrderItem)
                .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

            if (delivery == null)
            {
                return ApiResponse<DeliveryResponse>.FailureResponse($"Delivery {deliveryId} was not found.");
            }

            return ApiResponse<DeliveryResponse>.SuccessResponse(MapToResponse(delivery));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching delivery {DeliveryId}", deliveryId);
            return ApiResponse<DeliveryResponse>.FailureResponse("An error occurred while fetching delivery details.");
        }
    }

    public async Task<ApiResponse<DeliveryResponse>> MarkDispatchedAsync(int deliveryId, MarkDispatchedRequest request, CancellationToken ct = default)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

            if (delivery == null)
            {
                return ApiResponse<DeliveryResponse>.FailureResponse($"Delivery {deliveryId} was not found.");
            }

            var previousStatus = delivery.Status;
            _statusGuard.EnsureCanTransition(previousStatus, DeliveryStatus.InTransit);

            var actor = _currentUser.Current;
            delivery.Status = DeliveryStatus.InTransit;
            delivery.DispatchedDate = request.DispatchedDate ?? DateTime.UtcNow;
            delivery.DispatchedBy = actor.AuditName;

            if (request.ExpectedArrivalDate.HasValue)
                delivery.ExpectedArrivalDate = request.ExpectedArrivalDate.Value;

            if (!string.IsNullOrWhiteSpace(request.TrackingNumber))
                delivery.TrackingNumber = request.TrackingNumber.Trim();

            if (!string.IsNullOrWhiteSpace(request.Carrier))
                delivery.Carrier = request.Carrier.Trim();

            if (!string.IsNullOrWhiteSpace(request.DriverName))
                delivery.DriverName = request.DriverName.Trim();

            if (!string.IsNullOrWhiteSpace(request.VehiclePlateNumber))
                delivery.VehiclePlateNumber = request.VehiclePlateNumber.Trim();

            if (!string.IsNullOrWhiteSpace(request.Notes))
                delivery.Notes = request.Notes.Trim();

            if (!string.IsNullOrWhiteSpace(request.AttachmentUrl))
                delivery.AttachmentUrl = request.AttachmentUrl.Trim();

            if (!string.IsNullOrWhiteSpace(request.DispatchAttachmentBase64))
                delivery.DispatchAttachment = request.DispatchAttachmentBase64.Trim();

            await _posting.ExecuteAsync(async () =>
            {
                await _context.SaveChangesAsync(ct);
                _audit.Record(
                    nameof(Delivery),
                    delivery.DeliveryId.ToString(),
                    "Dispatched",
                    nameof(Delivery.Status),
                    EnumDbValue.ToDbValue(previousStatus),
                    EnumDbValue.ToDbValue(DeliveryStatus.InTransit));
            });

            return await GetDeliveryByIdAsync(delivery.DeliveryId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking delivery {DeliveryId} as dispatched", deliveryId);
            return ApiResponse<DeliveryResponse>.FailureResponse($"Failed to dispatch delivery: {ex.Message}");
        }
    }

    public async Task<ApiResponse<DeliveryResponse>> MarkArrivedAsync(int deliveryId, MarkArrivedRequest request, CancellationToken ct = default)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

            if (delivery == null)
            {
                return ApiResponse<DeliveryResponse>.FailureResponse($"Delivery {deliveryId} was not found.");
            }

            var previousStatus = delivery.Status;
            _statusGuard.EnsureCanTransition(previousStatus, DeliveryStatus.Arrived);

            var actor = _currentUser.Current;
            delivery.Status = DeliveryStatus.Arrived;
            delivery.ActualArrivalDate = request.ActualArrivalDate ?? DateTime.UtcNow;
            delivery.ArrivalCondition = string.IsNullOrWhiteSpace(request.ArrivalCondition) ? "Normal" : request.ArrivalCondition.Trim();
            delivery.ArrivalNotes = request.ArrivalNotes?.Trim();
            delivery.ReceivedBy = !string.IsNullOrWhiteSpace(request.ReceivedBy) ? request.ReceivedBy.Trim() : actor.AuditName;

            if (!string.IsNullOrWhiteSpace(request.DeliveryNoteNumber))
                delivery.DeliveryNoteNumber = request.DeliveryNoteNumber.Trim();

            if (!string.IsNullOrWhiteSpace(request.Notes))
                delivery.Notes = request.Notes.Trim();

            if (!string.IsNullOrWhiteSpace(request.AttachmentUrl))
                delivery.AttachmentUrl = request.AttachmentUrl.Trim();

            if (!string.IsNullOrWhiteSpace(request.ArrivalAttachmentBase64))
                delivery.ArrivalAttachment = request.ArrivalAttachmentBase64.Trim();

            await _posting.ExecuteAsync(async () =>
            {
                await _context.SaveChangesAsync(ct);
                _audit.Record(
                    nameof(Delivery),
                    delivery.DeliveryId.ToString(),
                    "Arrived",
                    nameof(Delivery.Status),
                    EnumDbValue.ToDbValue(previousStatus),
                    EnumDbValue.ToDbValue(DeliveryStatus.Arrived));
            });

            return await GetDeliveryByIdAsync(delivery.DeliveryId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking delivery {DeliveryId} as arrived", deliveryId);
            return ApiResponse<DeliveryResponse>.FailureResponse($"Failed to mark delivery as arrived: {ex.Message}");
        }
    }

    public async Task<ApiResponse<DeliveryResponse>> CancelDeliveryAsync(int deliveryId, string? reason = null, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return ApiResponse<DeliveryResponse>.FailureResponse("A cancellation reason is required before cancelling the delivery order.");
            }

            var delivery = await _context.Deliveries
                .Include(d => d.PurchaseOrder)
                .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

            if (delivery == null)
            {
                return ApiResponse<DeliveryResponse>.FailureResponse($"Delivery {deliveryId} was not found.");
            }

            var previousStatus = delivery.Status;
            _statusGuard.EnsureCanTransition(previousStatus, DeliveryStatus.Cancelled);

            delivery.Status = DeliveryStatus.Cancelled;
            delivery.Notes = string.IsNullOrWhiteSpace(delivery.Notes)
                ? $"Cancellation Reason: {reason.Trim()}"
                : $"{delivery.Notes} | Cancellation Reason: {reason.Trim()}";

            await _posting.ExecuteAsync(async () =>
            {
                await _context.SaveChangesAsync(ct);
                _audit.Record(
                    nameof(Delivery),
                    delivery.DeliveryId.ToString(),
                    "Cancelled",
                    nameof(Delivery.Status),
                    EnumDbValue.ToDbValue(previousStatus),
                    EnumDbValue.ToDbValue(DeliveryStatus.Cancelled));
            });

            return await GetDeliveryByIdAsync(delivery.DeliveryId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling delivery {DeliveryId}", deliveryId);
            return ApiResponse<DeliveryResponse>.FailureResponse($"Failed to cancel delivery: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<DeliveryItemResponse>>> GetOutstandingPoItemsAsync(int poId, CancellationToken ct = default)
    {
        try
        {
            var po = await _context.PurchaseOrders
                .Include(p => p.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                .Include(p => p.PurchaseOrderItems)
                    .ThenInclude(poi => poi.PurchaseUom)
                .FirstOrDefaultAsync(p => p.PoId == poId, ct);

            if (po == null)
            {
                return ApiResponse<List<DeliveryItemResponse>>.FailureResponse($"Purchase order {poId} was not found.");
            }

            var itemResponses = new List<DeliveryItemResponse>();

            foreach (var poi in po.PurchaseOrderItems)
            {
                var inFlightQuantity = await _context.DeliveryItems
                    .Where(di => di.PoItemId == poi.PoItemId &&
                                 (di.Delivery.Status == DeliveryStatus.Scheduled || di.Delivery.Status == DeliveryStatus.InTransit || di.Delivery.Status == DeliveryStatus.Arrived))
                    .SumAsync(di => (decimal?)di.DeclaredQuantity, ct) ?? 0m;

                var outstanding = Math.Max(0m, poi.PoItemQuantity - poi.ReceivedQuantity - inFlightQuantity);

                itemResponses.Add(new DeliveryItemResponse
                {
                    PoItemId = poi.PoItemId,
                    ItemId = poi.ItemId,
                    ItemName = poi.Item?.ItemName ?? string.Empty,
                    ItemCode = poi.Item?.ItemCode ?? string.Empty,
                    PoOrderedQuantity = poi.PoItemQuantity,
                    PoTotalReceivedQuantity = poi.ReceivedQuantity,
                    PoOutstandingQuantity = outstanding,
                    AlreadyScheduledQuantity = inFlightQuantity,
                    DeclaredQuantity = outstanding, // default suggested quantity is the remaining outstanding
                    PurchaseUomId = poi.PurchaseUomId,
                    PurchaseUomName = poi.PurchaseUom?.Name ?? poi.PurchaseUom?.Abbreviation ?? string.Empty
                });
            }

            return ApiResponse<List<DeliveryItemResponse>>.SuccessResponse(itemResponses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching outstanding items for PO {PoId}", poId);
            return ApiResponse<List<DeliveryItemResponse>>.FailureResponse("An error occurred while calculating outstanding PO items.");
        }
    }

    private static DeliveryResponse MapToResponse(Delivery d)
    {
        var grn = d.GoodsReceipts?.FirstOrDefault();
        return new DeliveryResponse
        {
            DeliveryId = d.DeliveryId,
            DeliveryNumber = d.DeliveryNumber,
            PoId = d.PoId,
            PoNumber = d.PurchaseOrder?.PoNumber ?? string.Empty,
            SupplierId = d.SupplierId,
            SupplierName = d.Supplier?.CompanyName ?? string.Empty,
            PaymentType = !string.IsNullOrWhiteSpace(d.PaymentType) ? d.PaymentType : (d.PurchaseOrder?.PaymentType ?? string.Empty),
            ReceivingLocationId = d.ReceivingLocationId,
            ReceivingLocationName = d.ReceivingLocation?.LocationName ?? string.Empty,
            Status = EnumDbValue.ToDbValue(d.Status),
            ScheduledDate = d.ScheduledDate,
            ExpectedArrivalDate = d.ExpectedArrivalDate,
            DispatchedDate = d.DispatchedDate,
            ActualArrivalDate = d.ActualArrivalDate,
            TrackingNumber = d.TrackingNumber,
            Carrier = d.Carrier,
            DriverName = d.DriverName,
            VehiclePlateNumber = d.VehiclePlateNumber,
            DeliveryNoteNumber = d.DeliveryNoteNumber,
            ArrivalCondition = d.ArrivalCondition,
            ArrivalNotes = d.ArrivalNotes,
            Notes = d.Notes,
            AttachmentUrl = d.AttachmentUrl,
            ScheduledAttachment = d.ScheduledAttachment,
            DispatchAttachment = d.DispatchAttachment,
            ArrivalAttachment = d.ArrivalAttachment,
            CreatedBy = d.CreatedBy,
            DispatchedBy = d.DispatchedBy,
            ReceivedBy = d.ReceivedBy,
            CreatedAt = d.CreatedAt,
            GrnId = grn?.GrnId,
            GrnNumber = grn?.GrnNumber,
            Items = d.Items?.Select(i => new DeliveryItemResponse
            {
                DeliveryItemId = i.DeliveryItemId,
                PoItemId = i.PoItemId,
                ItemId = i.ItemId,
                ItemName = i.Item?.ItemName ?? string.Empty,
                ItemCode = i.Item?.ItemCode ?? string.Empty,
                PoOrderedQuantity = i.PurchaseOrderItem?.PoItemQuantity ?? 0m,
                PoTotalReceivedQuantity = i.PurchaseOrderItem?.ReceivedQuantity ?? 0m,
                PoOutstandingQuantity = Math.Max(0m, (i.PurchaseOrderItem?.PoItemQuantity ?? 0m) - (i.PurchaseOrderItem?.ReceivedQuantity ?? 0m)),
                DeclaredQuantity = i.DeclaredQuantity,
                PurchaseUomId = i.PurchaseUomId,
                PurchaseUomName = i.PurchaseUom?.Name ?? i.PurchaseUom?.Abbreviation ?? string.Empty
            }).ToList() ?? new()
        };
    }
}
