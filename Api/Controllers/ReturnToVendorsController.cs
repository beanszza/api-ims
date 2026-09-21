using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Infrastructures.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReturnToVendorsController : ControllerBase
{
    private readonly ScmDbContext _context;

    public ReturnToVendorsController(ScmDbContext context)
    {
        _context = context;
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
                    returnedQuantity = r.ReturnedQuantity,
                    reason = r.Reason,
                    status = r.Status.ToString(),
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
                    returnedQuantity = r.ReturnedQuantity,
                    reason = r.Reason,
                    status = r.Status.ToString(),
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
}
