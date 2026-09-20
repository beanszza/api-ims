using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class SupplierScorecardService : ISupplierScorecardService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<SupplierScorecardService> _logger;

    public SupplierScorecardService(
        ScmDbContext context,
        ILogger<SupplierScorecardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<SupplierScorecardResponse>> GetSupplierScorecardAsync(int supplierId)
    {
        try
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

            if (supplier == null)
                return ApiResponse<SupplierScorecardResponse>.FailureResponse($"Supplier with ID {supplierId} not found.");

            var scorecard = await CalculateScorecardForSupplierAsync(supplier);
            return ApiResponse<SupplierScorecardResponse>.SuccessResponse(scorecard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating scorecard for supplier {SupplierId}", supplierId);
            return ApiResponse<SupplierScorecardResponse>.FailureResponse("An error occurred while generating the supplier scorecard.");
        }
    }

    public async Task<ApiResponse<List<SupplierScorecardResponse>>> GetAllSupplierScorecardsAsync()
    {
        try
        {
            var suppliers = await _context.Suppliers.ToListAsync();
            var list = new List<SupplierScorecardResponse>();

            foreach (var supplier in suppliers)
            {
                var scorecard = await CalculateScorecardForSupplierAsync(supplier);
                list.Add(scorecard);
            }

            return ApiResponse<List<SupplierScorecardResponse>>.SuccessResponse(
                list.OrderByDescending(s => s.CompositeScore).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier scorecards");
            return ApiResponse<List<SupplierScorecardResponse>>.FailureResponse("An error occurred while generating supplier scorecards.");
        }
    }

    private async Task<SupplierScorecardResponse> CalculateScorecardForSupplierAsync(Supplier supplier)
    {
        var supplierId = supplier.SupplierId;
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1. Purchase Orders & Spend
        var purchaseOrders = await _context.PurchaseOrders
            .Where(p => p.SupplierId == supplierId)
            .ToListAsync();

        var totalOrders = purchaseOrders.Count;
        var totalSpend = purchaseOrders.Sum(p => p.TotalAmount);

        // 2. Goods Receipts & On-Time Delivery
        var goodsReceipts = await _context.GoodsReceipts
            .Include(g => g.PurchaseOrder)
            .Where(g => g.SupplierId == supplierId)
            .ToListAsync();

        var totalReceipts = goodsReceipts.Count;
        decimal onTimeRate = 100m; // Default to 100% if no historical receipts

        if (totalReceipts > 0)
        {
            var onTimeCount = goodsReceipts.Count(g =>
                g.PurchaseOrder == null || g.ReceivedDate.Date <= g.PurchaseOrder.ExpectedArrivalDate.Date);

            onTimeRate = Math.Round(((decimal)onTimeCount / totalReceipts) * 100m, 1);
        }

        // 3. Quality Inspections & Acceptance Rate
        var grnIds = goodsReceipts.Select(g => g.GrnId).ToList();
        var qcInspections = await _context.QualityInspections
            .Include(q => q.Items)
            .Where(q => q.ReferenceType == "GRN" && grnIds.Contains(q.ReferenceId))
            .ToListAsync();

        var allQcItems = qcInspections.SelectMany(q => q.Items).ToList();
        decimal totalDelivered = allQcItems.Sum(i => i.DeliveredQuantity);
        decimal totalAccepted = allQcItems.Sum(i => i.AcceptedQuantity);
        decimal qualityRate = 100m; // Default to 100% if no inspections yet

        if (totalDelivered > 0)
        {
            qualityRate = Math.Round((totalAccepted / totalDelivered) * 100m, 1);
        }

        decimal defectRate = Math.Max(0m, Math.Round(100m - qualityRate, 1));

        // 4. NCRs & RTVs
        var ncrsCount = await _context.NonConformanceReports
            .CountAsync(n => n.SupplierId == supplierId);

        var rtvsCount = await _context.ReturnToVendors
            .CountAsync(r => r.SupplierId == supplierId);

        // 5. Compliance Documents
        var documents = await _context.SupplierDocuments
            .Where(d => d.SupplierId == supplierId)
            .ToListAsync();

        var hasFda = documents.Any(d =>
            d.DocumentType == SupplierDocumentType.FdaLto &&
            d.IsVerified &&
            (d.ExpiryDate == null || d.ExpiryDate >= today));

        var hasSanitary = documents.Any(d =>
            d.DocumentType == SupplierDocumentType.SanitaryPermit &&
            d.IsVerified &&
            (d.ExpiryDate == null || d.ExpiryDate >= today));

        var fullyCompliant = hasFda && hasSanitary;

        // 6. Composite Weighted Score
        // Quality: 40%, OTD: 35%, Compliance: 25%
        decimal complianceScore = fullyCompliant ? 100m : (hasFda || hasSanitary ? 50m : 0m);
        decimal composite = Math.Round((qualityRate * 0.40m) + (onTimeRate * 0.35m) + (complianceScore * 0.25m), 1);

        // 7. Performance Grade
        string grade = composite switch
        {
            >= 90m => "Grade A (Preferred Partner)",
            >= 80m => "Grade B (Qualified Supplier)",
            >= 70m => "Grade C (Conditional / Under Review)",
            _ => "Grade D (High Risk / Non-Compliant)"
        };

        string summary = fullyCompliant
            ? $"Supplier demonstrates {qualityRate}% QA acceptance and {onTimeRate}% OTD with valid FDA/Sanitary permits."
            : $"Compliance alert: Missing or expired regulatory permits (FDA: {(hasFda ? "Valid" : "Missing/Expired")}, Sanitary: {(hasSanitary ? "Valid" : "Missing/Expired")}).";

        return new SupplierScorecardResponse
        {
            SupplierId = supplier.SupplierId,
            CompanyName = supplier.CompanyName,
            ContactPerson = supplier.ContactPerson,
            Email = supplier.Email,
            Phone = supplier.Phone,
            TotalPurchaseOrders = totalOrders,
            TotalGoodsReceipts = totalReceipts,
            TotalSpend = totalSpend,
            OnTimeDeliveryRate = onTimeRate,
            QualityAcceptanceRate = qualityRate,
            DefectRate = defectRate,
            TotalNcrsFiled = ncrsCount,
            TotalRtvsDispatched = rtvsCount,
            IsFdaCompliant = hasFda,
            IsSanitaryCompliant = hasSanitary,
            IsFullyCompliant = fullyCompliant,
            CompositeScore = composite,
            PerformanceGrade = grade,
            EvaluationSummary = summary
        };
    }
}
