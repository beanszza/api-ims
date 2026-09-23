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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Api.Controllers;

public class RejectRtvRequest
{
    public string? Reason { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ReturnToVendorsController : ControllerBase
{
    private readonly ScmDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly INcrService _ncrService;

    public ReturnToVendorsController(
        ScmDbContext context,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        INcrService ncrService)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _ncrService = ncrService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetAll()
    {
        try
        {
            var list = await (from r in _context.ReturnToVendors
                join d in _context.Discrepancies.Include(x => x.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                    on r.RtvId equals d.RtvId into dGroup
                from d in dGroup.DefaultIfEmpty()
                join n in _context.NonConformanceReports on r.NcrId equals n.NcrId into nGroup
                from n in nGroup.DefaultIfEmpty()
                select new
                {
                    rtvId = r.RtvId,
                    rtvNumber = r.RtvNumber,
                    ncrId = r.NcrId,
                    ncrNumber = n != null ? n.NcrNumber : null,
                    discrepancyId = d != null ? (int?)d.DiscrepancyId : null,
                    discrepancyNumber = d != null ? d.DiscrepancyNumber : null,
                    grnId = d != null ? (int?)d.GrnId : null,
                    grnNumber = d != null ? d.GrnNumber : null,
                    poId = d != null ? (int?)d.PoId : null,
                    poNumber = d != null ? d.PoNumber : null,
                    prId = d != null && d.PurchaseOrder != null ? d.PurchaseOrder.PrId : null,
                    prNumber = d != null && d.PurchaseOrder != null && d.PurchaseOrder.PurchaseRequisition != null ? d.PurchaseOrder.PurchaseRequisition.PrNumber : null,
                    supplierId = r.SupplierId,
                    supplierName = r.Supplier.CompanyName,
                    itemId = r.ItemId,
                    itemName = r.Item.ItemName,
                    uomName = r.Item.Uom != null ? r.Item.Uom.Abbreviation : "Unit",
                    returnedQuantity = r.ReturnedQuantity,
                    quantityReturned = r.ReturnedQuantity,
                    reason = r.Reason,
                    status = r.Status.ToString(),
                    approvalRequestNotes = r.ApprovalRequestNotes,
                    approvedBy = r.ApprovedBy,
                    approvedAt = r.ApprovedAt,
                    rejectedBy = r.RejectedBy,
                    rejectedAt = r.RejectedAt,
                    rejectionReason = r.RejectionReason,
                    createdAt = r.CreatedAt,
                    dispatchedDate = r.DispatchedDate,
                    creditNoteNumber = r.CreditNoteNumber
                })
                .OrderByDescending(r => r.rtvId)
                .ToListAsync();

            return Ok(ApiResponse<List<object>>.SuccessResponse(list.Cast<object>().ToList()));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<object>>.FailureResponse($"Failed to fetch return shipments: {ex.Message}"));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        try
        {
            var item = await (from r in _context.ReturnToVendors
                join d in _context.Discrepancies.Include(x => x.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                    on r.RtvId equals d.RtvId into dGroup
                from d in dGroup.DefaultIfEmpty()
                join n in _context.NonConformanceReports on r.NcrId equals n.NcrId into nGroup
                from n in nGroup.DefaultIfEmpty()
                where r.RtvId == id
                select new
                {
                    rtvId = r.RtvId,
                    rtvNumber = r.RtvNumber,
                    ncrId = r.NcrId,
                    ncrNumber = n != null ? n.NcrNumber : null,
                    discrepancyId = d != null ? (int?)d.DiscrepancyId : null,
                    discrepancyNumber = d != null ? d.DiscrepancyNumber : null,
                    grnId = d != null ? (int?)d.GrnId : null,
                    grnNumber = d != null ? d.GrnNumber : null,
                    poId = d != null ? (int?)d.PoId : null,
                    poNumber = d != null ? d.PoNumber : null,
                    prId = d != null && d.PurchaseOrder != null ? d.PurchaseOrder.PrId : null,
                    prNumber = d != null && d.PurchaseOrder != null && d.PurchaseOrder.PurchaseRequisition != null ? d.PurchaseOrder.PurchaseRequisition.PrNumber : null,
                    supplierId = r.SupplierId,
                    supplierName = r.Supplier.CompanyName,
                    itemId = r.ItemId,
                    itemName = r.Item.ItemName,
                    uomName = r.Item.Uom != null ? r.Item.Uom.Abbreviation : "Unit",
                    returnedQuantity = r.ReturnedQuantity,
                    quantityReturned = r.ReturnedQuantity,
                    reason = r.Reason,
                    status = r.Status.ToString(),
                    approvalRequestNotes = r.ApprovalRequestNotes,
                    approvedBy = r.ApprovedBy,
                    approvedAt = r.ApprovedAt,
                    rejectedBy = r.RejectedBy,
                    rejectedAt = r.RejectedAt,
                    rejectionReason = r.RejectionReason,
                    createdAt = r.CreatedAt,
                    dispatchedDate = r.DispatchedDate,
                    creditNoteNumber = r.CreditNoteNumber
                })
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound(ApiResponse<object>.FailureResponse($"Return shipment {id} not found."));

            return Ok(ApiResponse<object>.SuccessResponse(item));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse($"Failed to fetch return shipment: {ex.Message}"));
        }
    }

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> ApproveRtv(int id)
    {
        try
        {
            var rtv = await _context.ReturnToVendors.FindAsync(id);
            if (rtv == null)
                return NotFound(ApiResponse<object>.FailureResponse($"Return shipment {id} not found."));

            if (rtv.Status != RtvStatus.PendingApproval)
                return BadRequest(ApiResponse<object>.FailureResponse($"Cannot approve return shipment in '{rtv.Status}' status."));

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            rtv.Status = RtvStatus.PendingDispatch;
            rtv.ApprovedBy = actor.AuditName;
            rtv.ApprovedAt = now;

            await _context.SaveChangesAsync();
            _audit.Record(nameof(ReturnToVendor), rtv.RtvNumber, "Approved", "Status", "PendingApproval", "PendingDispatch");

            return await GetById(id);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse($"Failed to approve return shipment: {ex.Message}"));
        }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> RejectRtv(int id, [FromBody] RejectRtvRequest request)
    {
        try
        {
            var rtv = await _context.ReturnToVendors.FindAsync(id);
            if (rtv == null)
                return NotFound(ApiResponse<object>.FailureResponse($"Return shipment {id} not found."));

            if (rtv.Status != RtvStatus.PendingApproval)
                return BadRequest(ApiResponse<object>.FailureResponse($"Cannot reject return shipment in '{rtv.Status}' status."));

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            rtv.Status = RtvStatus.Cancelled;
            rtv.RejectedBy = actor.AuditName;
            rtv.RejectedAt = now;
            rtv.RejectionReason = request.Reason ?? "Rejected by administrator";

            await _context.SaveChangesAsync();
            _audit.Record(nameof(ReturnToVendor), rtv.RtvNumber, "Rejected", "Status", "PendingApproval", "Cancelled");

            return await GetById(id);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse($"Failed to reject return shipment: {ex.Message}"));
        }
    }

    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<ApiResponse<object>>> DispatchRtv(int id, [FromBody] DispatchRtvRequest request)
    {
        try
        {
            var rtv = await _context.ReturnToVendors.FindAsync(id);
            if (rtv == null)
                return NotFound(ApiResponse<object>.FailureResponse($"Return shipment {id} not found."));

            if (rtv.Status != RtvStatus.PendingDispatch)
                return BadRequest(ApiResponse<object>.FailureResponse($"Cannot dispatch return shipment in '{rtv.Status}' status. Must be approved first."));

            var result = await _ncrService.DispatchRtvAsync(id, request);
            if (!result.Success)
                return BadRequest(ApiResponse<object>.FailureResponse(result.Message));

            return await GetById(id);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.FailureResponse($"Failed to dispatch return shipment: {ex.Message}"));
        }
    }
}
