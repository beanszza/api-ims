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
public class LossReportsController : ControllerBase
{
    private readonly ScmDbContext _context;

    public LossReportsController(ScmDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<LossReportResponse>>>> GetAll()
    {
        try
        {
            var list = await _context.LossReports
                .Include(l => l.Item).ThenInclude(i => i.Uom)
                .Include(l => l.Discrepancy)
                .Include(l => l.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                .Include(l => l.GoodsReceipt).ThenInclude(g => g.Supplier)
                .OrderByDescending(l => l.LossReportId)
                .Select(l => new LossReportResponse
                {
                    LossReportId = l.LossReportId,
                    LossReportNumber = l.LossReportNumber,
                    DiscrepancyId = l.DiscrepancyId,
                    DiscrepancyNumber = l.Discrepancy != null ? l.Discrepancy.DiscrepancyNumber : null,
                    GrnId = l.GrnId,
                    GrnNumber = l.GoodsReceipt != null ? l.GoodsReceipt.GrnNumber : null,
                    PoId = l.PoId,
                    PoNumber = l.GoodsReceipt != null && l.GoodsReceipt.PurchaseOrder != null ? l.GoodsReceipt.PurchaseOrder.PoNumber : $"PO-{l.PoId}",
                    PrId = l.GoodsReceipt != null && l.GoodsReceipt.PurchaseOrder != null ? l.GoodsReceipt.PurchaseOrder.PrId : null,
                    PrNumber = l.GoodsReceipt != null && l.GoodsReceipt.PurchaseOrder != null && l.GoodsReceipt.PurchaseOrder.PurchaseRequisition != null ? l.GoodsReceipt.PurchaseOrder.PurchaseRequisition.PrNumber : null,
                    SupplierName = l.GoodsReceipt != null && l.GoodsReceipt.Supplier != null ? l.GoodsReceipt.Supplier.CompanyName : null,
                    ItemId = l.ItemId,
                    ItemName = l.Item.ItemName,
                    LostQuantity = l.LostQuantity,
                    UomId = l.UomId,
                    UomName = l.Item.Uom != null ? l.Item.Uom.Abbreviation : "Unit",
                    Reason = l.Reason,
                    Notes = l.Notes,
                    AuthorisedBy = l.AuthorisedBy,
                    CreatedBy = l.CreatedBy,
                    CreatedAt = l.CreatedAt,
                    StockLedgerEntryId = l.StockLedgerEntryId
                })
                .ToListAsync();

            return Ok(ApiResponse<List<LossReportResponse>>.SuccessResponse(list));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<List<LossReportResponse>>.FailureResponse($"Failed to retrieve loss reports: {ex.Message}"));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<LossReportResponse>>> GetById(int id)
    {
        try
        {
            var l = await _context.LossReports
                .Include(l => l.Item).ThenInclude(i => i.Uom)
                .Include(l => l.Discrepancy)
                .Include(l => l.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                .Include(l => l.GoodsReceipt).ThenInclude(g => g.Supplier)
                .FirstOrDefaultAsync(l => l.LossReportId == id);

            if (l == null)
                return NotFound(ApiResponse<LossReportResponse>.FailureResponse($"Loss report {id} not found."));

            var resp = new LossReportResponse
            {
                LossReportId = l.LossReportId,
                LossReportNumber = l.LossReportNumber,
                DiscrepancyId = l.DiscrepancyId,
                DiscrepancyNumber = l.Discrepancy?.DiscrepancyNumber,
                GrnId = l.GrnId,
                GrnNumber = l.GoodsReceipt?.GrnNumber,
                PoId = l.PoId,
                PoNumber = l.GoodsReceipt?.PurchaseOrder?.PoNumber ?? $"PO-{l.PoId}",
                PrId = l.GoodsReceipt?.PurchaseOrder?.PrId,
                PrNumber = l.GoodsReceipt?.PurchaseOrder?.PurchaseRequisition?.PrNumber,
                SupplierName = l.GoodsReceipt?.Supplier?.CompanyName,
                ItemId = l.ItemId,
                ItemName = l.Item.ItemName,
                LostQuantity = l.LostQuantity,
                UomId = l.UomId,
                UomName = l.Item.Uom?.Abbreviation ?? "Unit",
                Reason = l.Reason,
                Notes = l.Notes,
                AuthorisedBy = l.AuthorisedBy,
                CreatedBy = l.CreatedBy,
                CreatedAt = l.CreatedAt,
                StockLedgerEntryId = l.StockLedgerEntryId
            };

            return Ok(ApiResponse<LossReportResponse>.SuccessResponse(resp));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<LossReportResponse>.FailureResponse($"Failed to retrieve loss report: {ex.Message}"));
        }
    }
}
