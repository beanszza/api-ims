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

public class PurchaseRequisitionService : IPurchaseRequisitionService
{
    private readonly ScmDbContext _context;
    private readonly IPurchaseOrderService _poService;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<PurchaseRequisitionService> _logger;

    public PurchaseRequisitionService(
        ScmDbContext context,
        IPurchaseOrderService poService,
        IDocumentNumberService documentNumbers,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<PurchaseRequisitionService> logger)
    {
        _context = context;
        _poService = poService;
        _documentNumbers = documentNumbers;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<PurchaseRequisitionResponse>>> GetPurchaseRequisitionsAsync(string? status = null)
    {
        try
        {
            var query = _context.PurchaseRequisitions
                .Include(p => p.Items)
                    .ThenInclude(i => i.Item)
                .Include(p => p.Items)
                    .ThenInclude(i => i.SuggestedSupplier)
                .Include(p => p.Items)
                    .ThenInclude(i => i.PurchaseUom)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<PurchaseRequisitionStatus>(status, out var st))
                {
                    query = query.Where(p => p.Status == st);
                }
            }

            var list = await query.OrderByDescending(p => p.PrId).ToListAsync();
            return ApiResponse<List<PurchaseRequisitionResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase requisitions");
            return ApiResponse<List<PurchaseRequisitionResponse>>.FailureResponse("An error occurred while fetching purchase requisitions.");
        }
    }

    public async Task<ApiResponse<PurchaseRequisitionResponse>> GetPurchaseRequisitionByIdAsync(int prId)
    {
        try
        {
            var pr = await _context.PurchaseRequisitions
                .Include(p => p.Items)
                    .ThenInclude(i => i.Item)
                .Include(p => p.Items)
                    .ThenInclude(i => i.SuggestedSupplier)
                .Include(p => p.Items)
                    .ThenInclude(i => i.PurchaseUom)
                .FirstOrDefaultAsync(p => p.PrId == prId);

            if (pr == null)
                return ApiResponse<PurchaseRequisitionResponse>.FailureResponse($"Purchase requisition {prId} not found.");

            return ApiResponse<PurchaseRequisitionResponse>.SuccessResponse(MapToResponse(pr));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PR {PrId}", prId);
            return ApiResponse<PurchaseRequisitionResponse>.FailureResponse("An error occurred while fetching purchase requisition.");
        }
    }

    public async Task<ApiResponse<PurchaseRequisitionResponse>> CreatePurchaseRequisitionAsync(CreatePurchaseRequisitionRequest request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<PurchaseRequisitionResponse>.FailureResponse("Requisition must contain at least one item.");

            var actor = _currentUser.Current;
            var reqDate = DateTime.UtcNow;

            var prItems = new List<PurchaseRequisitionItem>();
            foreach (var itemReq in request.Items)
            {
                var item = await _context.Items.FindAsync(itemReq.ItemId);
                if (item == null)
                    return ApiResponse<PurchaseRequisitionResponse>.FailureResponse($"Item {itemReq.ItemId} not found.");

                var uomId = itemReq.PurchaseUomId ?? item.StockUomId;
                var unitPrice = itemReq.EstimatedUnitPrice ?? 0m;

                if (unitPrice <= 0)
                {
                    var catalogEntry = await _context.SupplierItems
                        .FirstOrDefaultAsync(si => si.ItemId == itemReq.ItemId && (itemReq.SuggestedSupplierId == null || si.SupplierId == itemReq.SuggestedSupplierId));
                    if (catalogEntry != null)
                    {
                        unitPrice = catalogEntry.UnitPrice;
                    }
                }

                prItems.Add(new PurchaseRequisitionItem
                {
                    ItemId = itemReq.ItemId,
                    SuggestedSupplierId = itemReq.SuggestedSupplierId,
                    RequestedQuantity = itemReq.RequestedQuantity,
                    PurchaseUomId = uomId,
                    EstimatedUnitPrice = unitPrice
                });
            }

            var estTotal = prItems.Sum(i => i.RequestedQuantity * i.EstimatedUnitPrice);
            var reqDateNow = DateTime.UtcNow;

            var prNumber = await _documentNumbers.NextAsync(DocumentType.PurchaseRequisition, reqDateNow);

            var pr = new PurchaseRequisition
            {
                PrNumber = prNumber,
                Department = request.Department.Trim(),
                RequestedBy = actor.AuditName,
                RequestDate = reqDateNow,
                RequiredDate = request.RequiredDate != default ? request.RequiredDate : reqDateNow.AddDays(7),
                Status = PurchaseRequisitionStatus.Draft,
                Purpose = request.Purpose,
                EstimatedTotalAmount = estTotal,
                Items = prItems
            };

            _context.PurchaseRequisitions.Add(pr);
            await _context.SaveChangesAsync();

            var reloaded = await _context.PurchaseRequisitions
                .Include(p => p.Items).ThenInclude(i => i.Item)
                .Include(p => p.Items).ThenInclude(i => i.SuggestedSupplier)
                .Include(p => p.Items).ThenInclude(i => i.PurchaseUom)
                .FirstAsync(p => p.PrId == pr.PrId);

            _audit.Record(
                nameof(PurchaseRequisition),
                pr.PrNumber,
                "RequisitionCreated",
                fieldName: "Status",
                oldValue: null,
                newValue: "Draft");

            return ApiResponse<PurchaseRequisitionResponse>.SuccessResponse(MapToResponse(reloaded), "Purchase requisition created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase requisition");
            return ApiResponse<PurchaseRequisitionResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseRequisitionResponse>> UpdateStatusAsync(int prId, UpdatePurchaseRequisitionStatusRequest request)
    {
        try
        {
            var pr = await _context.PurchaseRequisitions
                .Include(p => p.Items).ThenInclude(i => i.Item)
                .Include(p => p.Items).ThenInclude(i => i.SuggestedSupplier)
                .Include(p => p.Items).ThenInclude(i => i.PurchaseUom)
                .FirstOrDefaultAsync(p => p.PrId == prId);

            if (pr == null)
                return ApiResponse<PurchaseRequisitionResponse>.FailureResponse($"Purchase requisition {prId} not found.");

            if (!EnumDbValue.TryParse<PurchaseRequisitionStatus>(request.Status, out var newStatus) || newStatus == PurchaseRequisitionStatus.Unspecified)
                return ApiResponse<PurchaseRequisitionResponse>.FailureResponse($"Invalid status '{request.Status}'. Allowed: {EnumDbValue.DescribeAccepted<PurchaseRequisitionStatus>()}.");

            var oldStatus = pr.Status;
            pr.Status = newStatus;

            _context.PurchaseRequisitions.Update(pr);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(PurchaseRequisition),
                pr.PrNumber,
                "StatusUpdated",
                fieldName: "Status",
                oldValue: EnumDbValue.ToDbValue(oldStatus),
                newValue: EnumDbValue.ToDbValue(newStatus));

            return ApiResponse<PurchaseRequisitionResponse>.SuccessResponse(MapToResponse(pr), $"Requisition status updated to {EnumDbValue.ToDbValue(newStatus)}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status on PR {PrId}", prId);
            return ApiResponse<PurchaseRequisitionResponse>.FailureResponse("An error occurred while updating status.");
        }
    }

    public async Task<ApiResponse<PrFanOutResultResponse>> FanOutToPurchaseOrdersAsync(int prId)
    {
        try
        {
            var pr = await _context.PurchaseRequisitions
                .Include(p => p.Items).ThenInclude(i => i.Item)
                .Include(p => p.Items).ThenInclude(i => i.SuggestedSupplier)
                .Include(p => p.Items).ThenInclude(i => i.PurchaseUom)
                .FirstOrDefaultAsync(p => p.PrId == prId);

            if (pr == null)
                return ApiResponse<PrFanOutResultResponse>.FailureResponse($"Purchase requisition {prId} not found.");

            if (pr.Status != PurchaseRequisitionStatus.Approved)
                return ApiResponse<PrFanOutResultResponse>.FailureResponse($"Only Approved requisitions can be converted to Purchase Orders. Current status: {EnumDbValue.ToDbValue(pr.Status)}.");

            // Group items by Supplier
            var supplierItemMap = new Dictionary<int, List<PurchaseRequisitionItem>>();

            foreach (var item in pr.Items)
            {
                int supplierId;
                if (item.SuggestedSupplierId.HasValue && item.SuggestedSupplierId.Value > 0)
                {
                    supplierId = item.SuggestedSupplierId.Value;
                }
                else
                {
                    // Find preferred vendor in catalog
                    var preferred = await _context.SupplierItems
                        .FirstOrDefaultAsync(si => si.ItemId == item.ItemId && si.IsPreferred && si.IsActive);

                    if (preferred != null)
                    {
                        supplierId = preferred.SupplierId;
                    }
                    else
                    {
                        var anyVendor = await _context.SupplierItems
                            .FirstOrDefaultAsync(si => si.ItemId == item.ItemId && si.IsActive);
                        if (anyVendor != null)
                        {
                            supplierId = anyVendor.SupplierId;
                        }
                        else
                        {
                            return ApiResponse<PrFanOutResultResponse>.FailureResponse(
                                $"Item '{item.Item?.ItemName ?? item.ItemId.ToString()}' has no registered supplier in catalog. Please specify a supplier.");
                        }
                    }
                }

                if (!supplierItemMap.TryGetValue(supplierId, out var itemList))
                {
                    itemList = new List<PurchaseRequisitionItem>();
                    supplierItemMap[supplierId] = itemList;
                }
                itemList.Add(item);
            }

            var generatedPos = new List<PurchaseOrderResponse>();
            var poNumbers = new List<string>();

            foreach (var (supplierId, items) in supplierItemMap)
            {
                var createPoReq = new CreatePurchaseOrderRequest
                {
                    SupplierId = supplierId,
                    ExpectedArrivalDate = pr.RequiredDate > DateTime.UtcNow ? pr.RequiredDate : DateTime.UtcNow.AddDays(3),
                    PaymentType = "Terms 30 Days",
                    Items = items.Select(i => new CreatePurchaseOrderItemRequest
                    {
                        ItemId = i.ItemId,
                        PoItemQuantity = i.RequestedQuantity,
                        UnitPrice = i.EstimatedUnitPrice,
                        PurchaseUomId = i.PurchaseUomId
                    }).ToList()
                };

                var poResult = await _poService.CreatePurchaseOrderAsync(createPoReq);
                if (!poResult.Success || poResult.Data == null)
                {
                    return ApiResponse<PrFanOutResultResponse>.FailureResponse(
                        $"Failed to generate PO for supplier {supplierId}: {poResult.Message}");
                }

                generatedPos.Add(poResult.Data);
                poNumbers.Add(poResult.Data.PoNumber);
            }

            // Update PR to ConvertedToPo
            pr.Status = PurchaseRequisitionStatus.ConvertedToPo;
            pr.GeneratedPoNumbers = string.Join(", ", poNumbers);
            _context.PurchaseRequisitions.Update(pr);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(PurchaseRequisition),
                pr.PrNumber,
                "ConvertedToPurchaseOrders",
                fieldName: "Status",
                oldValue: "Approved",
                newValue: "Converted to PO");

            var response = new PrFanOutResultResponse
            {
                PrId = pr.PrId,
                PrNumber = pr.PrNumber,
                PurchaseOrdersCreatedCount = generatedPos.Count,
                GeneratedPurchaseOrders = generatedPos
            };

            return ApiResponse<PrFanOutResultResponse>.SuccessResponse(
                response,
                $"Successfully fanned out requisition {pr.PrNumber} into {generatedPos.Count} supplier purchase orders ({pr.GeneratedPoNumbers}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during multi-supplier PR fan-out for PR {PrId}", prId);
            return ApiResponse<PrFanOutResultResponse>.FailureResponse($"An error occurred during PR fan-out: {ex.Message}");
        }
    }

    private static PurchaseRequisitionResponse MapToResponse(PurchaseRequisition p) => new()
    {
        PrId = p.PrId,
        PrNumber = p.PrNumber,
        Department = p.Department,
        RequestedBy = p.RequestedBy,
        RequestDate = p.RequestDate,
        RequiredDate = p.RequiredDate,
        Status = EnumDbValue.ToDbValue(p.Status),
        Purpose = p.Purpose,
        EstimatedTotalAmount = p.EstimatedTotalAmount,
        GeneratedPoNumbers = p.GeneratedPoNumbers,
        Items = p.Items.Select(i => new PurchaseRequisitionItemResponse
        {
            PrItemId = i.PrItemId,
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            SuggestedSupplierId = i.SuggestedSupplierId,
            SuggestedSupplierName = i.SuggestedSupplier?.CompanyName,
            RequestedQuantity = i.RequestedQuantity,
            PurchaseUomId = i.PurchaseUomId,
            PurchaseUomName = i.PurchaseUom?.Abbreviation ?? "Unit",
            EstimatedUnitPrice = i.EstimatedUnitPrice
        }).ToList()
    };
}
