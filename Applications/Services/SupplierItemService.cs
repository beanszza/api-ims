using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class SupplierItemService : ISupplierItemService
{
    private readonly ScmDbContext _context;
    private readonly IUomConversionService _uomService;
    private readonly ILogger<SupplierItemService> _logger;

    public SupplierItemService(
        ScmDbContext context,
        IUomConversionService uomService,
        ILogger<SupplierItemService> logger)
    {
        _context = context;
        _uomService = uomService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<SupplierItemResponse>>> GetItemsBySupplierAsync(int supplierId)
    {
        try
        {
            var supplierExists = await _context.Suppliers.AnyAsync(s => s.SupplierId == supplierId);
            if (!supplierExists)
                return ApiResponse<List<SupplierItemResponse>>.FailureResponse($"Supplier {supplierId} not found.");

            var items = await _context.SupplierItems
                .Where(si => si.SupplierId == supplierId)
                .Include(si => si.Supplier)
                .Include(si => si.Item)
                .Include(si => si.PurchaseUom)
                .OrderByDescending(si => si.IsPreferred)
                .ThenBy(si => si.Item != null ? si.Item.ItemName : "")
                .ToListAsync();

            var response = items.Select(MapToResponse).ToList();
            return ApiResponse<List<SupplierItemResponse>>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving catalog items for supplier {SupplierId}", supplierId);
            return ApiResponse<List<SupplierItemResponse>>.FailureResponse("An error occurred while fetching supplier items.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<ItemSupplierOptionResponse>>> GetSuppliersByItemAsync(int itemId)
    {
        try
        {
            var itemExists = await _context.Items.AnyAsync(i => i.ItemId == itemId);
            if (!itemExists)
                return ApiResponse<List<ItemSupplierOptionResponse>>.FailureResponse($"Item {itemId} not found.");

            var supplierItems = await _context.SupplierItems
                .Where(si => si.ItemId == itemId && si.IsActive && (si.Supplier == null || si.Supplier.IsActive))
                .Include(si => si.Supplier)
                .Include(si => si.PurchaseUom)
                .OrderByDescending(si => si.IsPreferred)
                .ThenBy(si => si.UnitPrice)
                .ToListAsync();

            var options = supplierItems.Select(si => new ItemSupplierOptionResponse
            {
                SupplierId = si.SupplierId,
                SupplierName = si.Supplier?.CompanyName ?? $"Supplier {si.SupplierId}",
                UnitPrice = si.UnitPrice,
                Currency = si.Currency,
                PurchaseUomId = si.PurchaseUomId,
                PurchaseUomName = si.PurchaseUom?.Abbreviation ?? si.PurchaseUom?.Name ?? "Unit",
                PackSize = si.PackSize,
                LeadTimeDays = si.LeadTimeDays,
                MinOrderQuantity = si.MinOrderQuantity,
                IsPreferred = si.IsPreferred
            }).ToList();

            return ApiResponse<List<ItemSupplierOptionResponse>>.SuccessResponse(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier options for item {ItemId}", itemId);
            return ApiResponse<List<ItemSupplierOptionResponse>>.FailureResponse("An error occurred while fetching item suppliers.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SupplierItemResponse>> GetSupplierItemAsync(int supplierId, int itemId)
    {
        try
        {
            var si = await _context.SupplierItems
                .Include(s => s.Supplier)
                .Include(s => s.Item)
                .Include(s => s.PurchaseUom)
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId && s.ItemId == itemId);

            if (si == null)
                return ApiResponse<SupplierItemResponse>.FailureResponse($"No catalog record found for Supplier {supplierId} and Item {itemId}.");

            return ApiResponse<SupplierItemResponse>.SuccessResponse(MapToResponse(si));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving catalog record for Supplier {SupplierId} and Item {ItemId}", supplierId, itemId);
            return ApiResponse<SupplierItemResponse>.FailureResponse("An error occurred while retrieving catalog details.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<SupplierItemResponse>> UpsertSupplierItemAsync(CreateOrUpdateSupplierItemRequest request)
    {
        try
        {
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
            if (supplier == null)
                return ApiResponse<SupplierItemResponse>.FailureResponse($"Supplier with ID {request.SupplierId} not found.");

            var item = await _context.Items.FindAsync(request.ItemId);
            if (item == null)
                return ApiResponse<SupplierItemResponse>.FailureResponse($"Item with ID {request.ItemId} not found.");

            // Resolve purchase UoM
            var purchaseUomId = request.PurchaseUomId > 0 ? request.PurchaseUomId : item.StockUomId;
            var uom = await _context.UnitOfMeasures.FindAsync(purchaseUomId);
            if (uom == null)
                return ApiResponse<SupplierItemResponse>.FailureResponse($"Unit of Measure with ID {purchaseUomId} not found.");

            // Dimension validation
            if (!await _uomService.CanConvertAsync(purchaseUomId, item.StockUomId))
            {
                return ApiResponse<SupplierItemResponse>.FailureResponse(
                    $"Purchase unit '{uom.Abbreviation}' cannot be converted to Item's stocking unit. Ensure dimensions match.");
            }

            var existing = await _context.SupplierItems
                .Include(s => s.Supplier)
                .Include(s => s.Item)
                .Include(s => s.PurchaseUom)
                .FirstOrDefaultAsync(si => si.SupplierId == request.SupplierId && si.ItemId == request.ItemId);

            if (request.IsPreferred)
            {
                // Unmark any other preferred suppliers for this item
                var otherPreferred = await _context.SupplierItems
                    .Where(si => si.ItemId == request.ItemId && si.SupplierId != request.SupplierId && si.IsPreferred)
                    .ToListAsync();
                foreach (var op in otherPreferred)
                {
                    op.IsPreferred = false;
                }
            }

            if (existing != null)
            {
                existing.SupplierSku = request.SupplierSku;
                existing.SupplierItemName = request.SupplierItemName;
                existing.UnitPrice = request.UnitPrice;
                existing.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PHP" : request.Currency;
                existing.PurchaseUomId = purchaseUomId;
                existing.PackSize = request.PackSize > 0 ? request.PackSize : 1m;
                existing.LeadTimeDays = request.LeadTimeDays >= 0 ? request.LeadTimeDays : 3;
                existing.MinOrderQuantity = request.MinOrderQuantity > 0 ? request.MinOrderQuantity : 1m;
                existing.IsPreferred = request.IsPreferred;
                existing.IsActive = request.IsActive;

                _context.SupplierItems.Update(existing);
                await _context.SaveChangesAsync();

                return ApiResponse<SupplierItemResponse>.SuccessResponse(MapToResponse(existing), "Supplier catalog entry updated successfully.");
            }
            else
            {
                var entry = new SupplierItem
                {
                    SupplierId = request.SupplierId,
                    ItemId = request.ItemId,
                    SupplierSku = request.SupplierSku,
                    SupplierItemName = request.SupplierItemName,
                    UnitPrice = request.UnitPrice,
                    Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PHP" : request.Currency,
                    PurchaseUomId = purchaseUomId,
                    PackSize = request.PackSize > 0 ? request.PackSize : 1m,
                    LeadTimeDays = request.LeadTimeDays >= 0 ? request.LeadTimeDays : 3,
                    MinOrderQuantity = request.MinOrderQuantity > 0 ? request.MinOrderQuantity : 1m,
                    IsPreferred = request.IsPreferred,
                    IsActive = request.IsActive
                };

                _context.SupplierItems.Add(entry);
                await _context.SaveChangesAsync();

                // Reload for full navigations
                var created = await _context.SupplierItems
                    .Include(s => s.Supplier)
                    .Include(s => s.Item)
                    .Include(s => s.PurchaseUom)
                    .FirstAsync(si => si.SupplierId == request.SupplierId && si.ItemId == request.ItemId);

                return ApiResponse<SupplierItemResponse>.SuccessResponse(MapToResponse(created), "Supplier catalog entry created successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving supplier item catalog entry.");
            return ApiResponse<SupplierItemResponse>.FailureResponse("An error occurred while saving catalog entry.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> RemoveSupplierItemAsync(int supplierId, int itemId)
    {
        try
        {
            var existing = await _context.SupplierItems
                .FirstOrDefaultAsync(si => si.SupplierId == supplierId && si.ItemId == itemId);

            if (existing == null)
                return ApiResponse<bool>.FailureResponse("Catalog entry not found.");

            _context.SupplierItems.Remove(existing);
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Catalog entry removed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting catalog entry for Supplier {SupplierId} and Item {ItemId}", supplierId, itemId);
            return ApiResponse<bool>.FailureResponse("An error occurred while removing catalog entry.");
        }
    }

    private static SupplierItemResponse MapToResponse(SupplierItem si) => new()
    {
        SupplierId = si.SupplierId,
        SupplierName = si.Supplier?.CompanyName ?? $"Supplier {si.SupplierId}",
        ItemId = si.ItemId,
        ItemName = si.Item?.ItemName ?? $"Item {si.ItemId}",
        SupplierSku = si.SupplierSku,
        SupplierItemName = si.SupplierItemName,
        UnitPrice = si.UnitPrice,
        Currency = si.Currency,
        PurchaseUomId = si.PurchaseUomId,
        PurchaseUomName = si.PurchaseUom?.Abbreviation ?? si.PurchaseUom?.Name ?? "Unit",
        PackSize = si.PackSize,
        LeadTimeDays = si.LeadTimeDays,
        MinOrderQuantity = si.MinOrderQuantity,
        IsPreferred = si.IsPreferred,
        LastPurchasePrice = si.LastPurchasePrice,
        LastPurchaseDate = si.LastPurchaseDate,
        IsActive = si.IsActive
    };
}
