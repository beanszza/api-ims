using System;
using System.Collections.Generic;
using System.Linq;
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

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly ScmDbContext _context;
    private readonly IStockPostingService _stockPosting;
    private readonly IPostingTransaction _posting;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly ILocationResolver _locations;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<GoodsReceiptService> _logger;

    public GoodsReceiptService(
        ScmDbContext context,
        IStockPostingService stockPosting,
        IPostingTransaction posting,
        IDocumentNumberService documentNumbers,
        ILocationResolver locations,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<GoodsReceiptService> logger)
    {
        _context = context;
        _stockPosting = stockPosting;
        _posting = posting;
        _documentNumbers = documentNumbers;
        _locations = locations;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<GoodsReceiptResponse>>> GetGoodsReceiptsAsync(int? poId = null)
    {
        try
        {
            var query = _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Delivery)
                .Include(g => g.Supplier)
                .Include(g => g.ReceivingLocation)
                .Include(g => g.Items).ThenInclude(i => i.Item)
                .Include(g => g.Items).ThenInclude(i => i.PurchaseUom)
                .Include(g => g.Items).ThenInclude(i => i.Lot)
                .AsQueryable();

            if (poId.HasValue)
                query = query.Where(g => g.PoId == poId.Value);

            var list = await query.OrderByDescending(g => g.GrnId).ToListAsync();
            return ApiResponse<List<GoodsReceiptResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Goods Receipt Notes");
            return ApiResponse<List<GoodsReceiptResponse>>.FailureResponse("An error occurred while fetching goods receipt notes.");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> GetGoodsReceiptByIdAsync(int grnId)
    {
        try
        {
            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Delivery)
                .Include(g => g.Supplier)
                .Include(g => g.ReceivingLocation)
                .Include(g => g.Items).ThenInclude(i => i.Item)
                .Include(g => g.Items).ThenInclude(i => i.PurchaseUom)
                .Include(g => g.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(g => g.GrnId == grnId);

            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Goods receipt note {grnId} not found.");

            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(grn));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GRN {GrnId}", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse("An error occurred while fetching goods receipt note.");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> ReceiveGoodsAsync(CreateGoodsReceiptRequest request)
    {
        try
        {
            var po = await _context.PurchaseOrders
                .Include(p => p.PurchaseOrderItems).ThenInclude(poi => poi.Item)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.PoId == request.PoId);

            if (po == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Purchase order {request.PoId} not found.");

            if (po.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Rejected)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Cannot receive goods against a {po.Status} purchase order.");

            if (request.Items == null || !request.Items.Any())
                return ApiResponse<GoodsReceiptResponse>.FailureResponse("At least one line item must be received.");

            var actor = _currentUser.Current;
            var receiveDate = DateTime.UtcNow;
            var receivingLocation = await _locations.ResolveReceivingLocationAsync(request.ReceivingLocationId);
            var receivingLocationId = receivingLocation.LocationId;

            var grnNumber = await _documentNumbers.NextAsync(DocumentType.GoodsReceipt, receiveDate);

            var grn = new GoodsReceipt
            {
                GrnNumber = grnNumber,
                PoId = po.PoId,
                DeliveryId = request.DeliveryId,
                SupplierId = po.SupplierId,
                ReceivingLocationId = receivingLocationId,
                ReceivedDate = receiveDate,
                DeliveryNoteNumber = request.DeliveryNoteNumber.Trim(),
                Carrier = request.Carrier,
                ReceivedBy = actor.AuditName,
                Status = GoodsReceiptStatus.Received,
                Notes = request.Notes
            };

            var grnItems = new List<GoodsReceiptItem>();

            await _posting.ExecuteAsync(async () =>
            {
                foreach (var itemReq in request.Items)
                {
                    var poItem = po.PurchaseOrderItems.FirstOrDefault(i => i.PoItemId == itemReq.PoItemId);
                    if (poItem == null)
                        throw new InvalidOperationException($"PO line item {itemReq.PoItemId} does not belong to purchase order {po.PoId}.");

                    if (itemReq.DeliveredQuantity <= 0)
                        throw new InvalidOperationException("Delivered quantity must be greater than zero.");

                    // Default manufacture date and expiry date if unsupplied
                    var mfgDate = itemReq.ManufactureDate ?? DateOnly.FromDateTime(DateTime.Today);
                    var expDate = itemReq.ExpiryDate ?? mfgDate.AddMonths(12);

                    // Physical receipt creates lot in Quarantine status
                    var receiveRequest = new ReceiveLotRequest(
                        ItemId: poItem.ItemId,
                        LocationId: receivingLocationId,
                        Quantity: itemReq.DeliveredQuantity,
                        SourceType: LotSourceType.Purchased,
                        Status: LotStatus.Quarantine)
                    {
                        SupplierId = po.SupplierId,
                        SupplierLotNo = itemReq.SupplierLotCode,
                        ManufactureDate = mfgDate,
                        ExpiryDate = expDate,
                        UnitCost = poItem.UnitPrice,
                        ReferenceType = nameof(GoodsReceipt),
                        ReferenceId = grnNumber,
                        Notes = $"Received via GRN {grnNumber} against PO {po.PoNumber}"
                    };

                    var lot = await _stockPosting.ReceiveAsync(receiveRequest);

                    // Update PO line received quantity
                    poItem.ReceivedQuantity += itemReq.DeliveredQuantity;

                    grnItems.Add(new GoodsReceiptItem
                    {
                        PoItemId = poItem.PoItemId,
                        ItemId = poItem.ItemId,
                        OrderedQuantity = poItem.PoItemQuantity,
                        DeliveredQuantity = itemReq.DeliveredQuantity,
                        PurchaseUomId = poItem.PurchaseUomId,
                        Lot = lot,
                        SupplierLotCode = itemReq.SupplierLotCode,
                        ManufactureDate = mfgDate,
                        ExpiryDate = expDate
                    });
                }

                grn.Items = grnItems;
                _context.GoodsReceipts.Add(grn);

                // Update PO status to Arrived if not already Completed/Arrived
                if (po.Status == PurchaseOrderStatus.Pending)
                {
                    po.Status = PurchaseOrderStatus.Arrived;
                    _context.PurchaseOrders.Update(po);
                }

                _audit.Record(
                    nameof(GoodsReceipt),
                    grn.GrnNumber,
                    "GoodsReceived",
                    fieldName: "Status",
                    oldValue: null,
                    newValue: "Received");

                return grn;
            });

            var reloaded = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Delivery)
                .Include(g => g.Supplier)
                .Include(g => g.ReceivingLocation)
                .Include(g => g.Items).ThenInclude(i => i.Item)
                .Include(g => g.Items).ThenInclude(i => i.PurchaseUom)
                .Include(g => g.Items).ThenInclude(i => i.Lot)
                .FirstAsync(g => g.GrnId == grn.GrnId);

            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(
                MapToResponse(reloaded),
                $"Goods Receipt Note {grn.GrnNumber} posted successfully. Stock placed in Quarantine pending QC inspection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing goods receipt for PO {PoId}", request.PoId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to receive goods: {ex.Message}");
        }
    }

    private static GoodsReceiptResponse MapToResponse(GoodsReceipt g) => new()
    {
        GrnId = g.GrnId,
        GrnNumber = g.GrnNumber,
        PoId = g.PoId,
        PoNumber = g.PurchaseOrder?.PoNumber ?? $"PO-{g.PoId}",
        DeliveryId = g.DeliveryId,
        DeliveryNumber = g.Delivery?.DeliveryNumber,
        SupplierId = g.SupplierId,
        SupplierName = g.Supplier?.CompanyName ?? string.Empty,
        ReceivingLocationId = g.ReceivingLocationId,
        ReceivingLocationName = g.ReceivingLocation?.LocationName ?? string.Empty,
        ReceivedDate = g.ReceivedDate,
        DeliveryNoteNumber = g.DeliveryNoteNumber,
        Carrier = g.Carrier,
        ReceivedBy = g.ReceivedBy,
        Status = EnumDbValue.ToDbValue(g.Status),
        Notes = g.Notes,
        Items = g.Items.Select(i => new GoodsReceiptItemResponse
        {
            GrnItemId = i.GrnItemId,
            PoItemId = i.PoItemId,
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            OrderedQuantity = i.OrderedQuantity,
            DeliveredQuantity = i.DeliveredQuantity,
            PurchaseUomId = i.PurchaseUomId,
            PurchaseUomName = i.PurchaseUom?.Abbreviation ?? "Unit",
            LotId = i.LotId,
            LotCode = i.Lot?.LotCode,
            SupplierLotCode = i.SupplierLotCode,
            ManufactureDate = i.ManufactureDate,
            ExpiryDate = i.ExpiryDate
        }).ToList()
    };
}
