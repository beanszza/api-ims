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

public class SupplierDocumentService : ISupplierDocumentService
{
    private readonly ScmDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SupplierDocumentService> _logger;

    public SupplierDocumentService(
        ScmDbContext context,
        ICurrentUserService currentUser,
        ILogger<SupplierDocumentService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<SupplierDocumentResponse>>> GetDocumentsBySupplierAsync(int supplierId)
    {
        try
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null)
                return ApiResponse<List<SupplierDocumentResponse>>.FailureResponse($"Supplier {supplierId} not found.");

            var docs = await _context.SupplierDocuments
                .Where(d => d.SupplierId == supplierId)
                .Include(d => d.Supplier)
                .OrderByDescending(d => d.ExpiryDate)
                .ToListAsync();

            var responses = docs.Select(MapToResponse).ToList();
            return ApiResponse<List<SupplierDocumentResponse>>.SuccessResponse(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for supplier {SupplierId}", supplierId);
            return ApiResponse<List<SupplierDocumentResponse>>.FailureResponse("An error occurred while fetching supplier documents.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SupplierDocumentResponse>> GetDocumentByIdAsync(int documentId)
    {
        try
        {
            var doc = await _context.SupplierDocuments
                .Include(d => d.Supplier)
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (doc == null)
                return ApiResponse<SupplierDocumentResponse>.FailureResponse($"Document {documentId} not found.");

            return ApiResponse<SupplierDocumentResponse>.SuccessResponse(MapToResponse(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document {DocumentId}", documentId);
            return ApiResponse<SupplierDocumentResponse>.FailureResponse("An error occurred while retrieving document.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SupplierComplianceSummaryResponse>> GetComplianceSummaryAsync(int supplierId)
    {
        try
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null)
                return ApiResponse<SupplierComplianceSummaryResponse>.FailureResponse($"Supplier {supplierId} not found.");

            var docs = await _context.SupplierDocuments
                .Where(d => d.SupplierId == supplierId)
                .Include(d => d.Supplier)
                .ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);

            var validLto = docs.Any(d => d.DocumentType == SupplierDocumentType.FdaLto && (!d.ExpiryDate.HasValue || d.ExpiryDate.Value >= today));
            var validSanitary = docs.Any(d => d.DocumentType == SupplierDocumentType.SanitaryPermit && (!d.ExpiryDate.HasValue || d.ExpiryDate.Value >= today));

            var expiredCount = docs.Count(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value < today);
            var expiringSoonCount = docs.Count(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value >= today && (d.ExpiryDate.Value.DayNumber - today.DayNumber) <= 30);

            var summary = new SupplierComplianceSummaryResponse
            {
                SupplierId = supplierId,
                SupplierName = supplier.CompanyName,
                HasValidFdaLto = validLto,
                HasValidSanitaryPermit = validSanitary,
                TotalDocumentsCount = docs.Count,
                ExpiredDocumentsCount = expiredCount,
                ExpiringSoonCount = expiringSoonCount,
                Documents = docs.Select(MapToResponse).ToList()
            };

            return ApiResponse<SupplierComplianceSummaryResponse>.SuccessResponse(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing compliance summary for supplier {SupplierId}", supplierId);
            return ApiResponse<SupplierComplianceSummaryResponse>.FailureResponse("An error occurred while computing compliance summary.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SupplierDocumentResponse>> CreateDocumentAsync(CreateSupplierDocumentRequest request)
    {
        try
        {
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
            if (supplier == null)
                return ApiResponse<SupplierDocumentResponse>.FailureResponse($"Supplier {request.SupplierId} not found.");

            if (!EnumDbValue.TryParse<SupplierDocumentType>(request.DocumentType, out var docType) || docType == SupplierDocumentType.Unspecified)
                return ApiResponse<SupplierDocumentResponse>.FailureResponse($"Invalid document type '{request.DocumentType}'. Accepted values: {EnumDbValue.DescribeAccepted<SupplierDocumentType>()}.");

            var doc = new SupplierDocument
            {
                SupplierId = request.SupplierId,
                DocumentType = docType,
                DocumentNumber = request.DocumentNumber.Trim(),
                Title = request.Title.Trim(),
                IssueDate = request.IssueDate != default ? request.IssueDate : DateOnly.FromDateTime(DateTime.Today),
                ExpiryDate = request.ExpiryDate,
                FileUrl = request.FileUrl,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                IsVerified = false
            };

            _context.SupplierDocuments.Add(doc);
            await _context.SaveChangesAsync();

            doc.Supplier = supplier;
            return ApiResponse<SupplierDocumentResponse>.SuccessResponse(MapToResponse(doc), "Supplier document registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document for supplier {SupplierId}", request.SupplierId);
            return ApiResponse<SupplierDocumentResponse>.FailureResponse("An error occurred while adding supplier document.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SupplierDocumentResponse>> VerifyDocumentAsync(VerifySupplierDocumentRequest request)
    {
        try
        {
            var doc = await _context.SupplierDocuments
                .Include(d => d.Supplier)
                .FirstOrDefaultAsync(d => d.DocumentId == request.DocumentId);

            if (doc == null)
                return ApiResponse<SupplierDocumentResponse>.FailureResponse($"Document {request.DocumentId} not found.");

            var actor = _currentUser.Current;

            doc.IsVerified = request.IsVerified;
            doc.VerifiedBy = request.IsVerified ? actor.AuditName : null;
            doc.VerifiedAt = request.IsVerified ? DateTime.UtcNow : null;
            if (!string.IsNullOrWhiteSpace(request.Notes))
                doc.Notes = request.Notes;

            _context.SupplierDocuments.Update(doc);
            await _context.SaveChangesAsync();

            return ApiResponse<SupplierDocumentResponse>.SuccessResponse(MapToResponse(doc), $"Document verification status updated to {request.IsVerified}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying document {DocumentId}", request.DocumentId);
            return ApiResponse<SupplierDocumentResponse>.FailureResponse("An error occurred while verifying supplier document.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteDocumentAsync(int documentId)
    {
        try
        {
            var doc = await _context.SupplierDocuments.FindAsync(documentId);
            if (doc == null)
                return ApiResponse<bool>.FailureResponse($"Document {documentId} not found.");

            _context.SupplierDocuments.Remove(doc);
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Supplier document deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return ApiResponse<bool>.FailureResponse("An error occurred while deleting document.");
        }
    }

    private static SupplierDocumentResponse MapToResponse(SupplierDocument doc)
    {
        var isExpired = doc.IsExpired;
        var daysLeft = doc.DaysUntilExpiry;

        string status;
        if (!doc.IsVerified) status = "Unverified";
        else if (isExpired) status = "Expired";
        else if (daysLeft.HasValue && daysLeft.Value <= 30) status = "Expiring Soon";
        else status = "Valid";

        return new SupplierDocumentResponse
        {
            DocumentId = doc.DocumentId,
            SupplierId = doc.SupplierId,
            SupplierName = doc.Supplier?.CompanyName ?? $"Supplier {doc.SupplierId}",
            DocumentType = EnumDbValue.ToDbValue(doc.DocumentType),
            DocumentNumber = doc.DocumentNumber,
            Title = doc.Title,
            IssueDate = doc.IssueDate,
            ExpiryDate = doc.ExpiryDate,
            FileUrl = doc.FileUrl,
            IsVerified = doc.IsVerified,
            VerifiedBy = doc.VerifiedBy,
            VerifiedAt = doc.VerifiedAt,
            Notes = doc.Notes,
            IsExpired = isExpired,
            DaysUntilExpiry = daysLeft,
            Status = status
        };
    }
}
