using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class FinishedProductService : IFinishedProductService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<FinishedProductService> _logger;

    public FinishedProductService(ScmDbContext context, ILogger<FinishedProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<FinishedProductResponse>>> GetAllFinishedProductsAsync()
    {
        try
        {
            var products = await _context.FinishedProducts
                .Include(fp => fp.Item)
                .Select(fp => new FinishedProductResponse
                {
                    ProductId = fp.ProductId,
                    ItemId = fp.ItemId,
                    SellingPrice = fp.SellingPrice,
                    Sku = fp.Sku,
                    ItemName = fp.Item != null ? fp.Item.ItemName : string.Empty,
                    Variant = fp.Variant,
                    ImageUrl = fp.ImageUrl ?? string.Empty
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<FinishedProductResponse>>.SuccessResponse(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching finished products.");
            return ApiResponse<IEnumerable<FinishedProductResponse>>.FailureResponse("An error occurred while fetching finished products.");
        }
    }

    public async Task<ApiResponse<FinishedProductResponse>> CreateFinishedProductAsync(api_scm.Contracts.Requests.CreateFinishedProductRequest request)
    {
        try
        {
            var trimmedName = request.ProductName?.Trim() ?? string.Empty;
            var existingItem = await _context.Items.FirstOrDefaultAsync(i => i.ItemName.ToLower() == trimmedName.ToLower());
            var item = existingItem;

            if (item == null)
            {
                var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryName.ToLower().Contains("finished good"));
                if (category == null)
                {
                    category = new Domains.Entities.Category { CategoryName = "Finished Good", Description = "Finished Goods" };
                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync();
                }

                var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Abbreviation == "pcs") 
                          ?? await _context.UnitOfMeasures.FirstOrDefaultAsync();

                item = new Domains.Entities.Item
                {
                    ItemName = trimmedName,
                    UomId = uom?.UomId ?? 1,
                    StockUomId = uom?.UomId ?? 1,
                    CategoryId = category?.CategoryId ?? 1,
                    MinStockLevel = 0,
                    MaxStockLevel = 100,
                    IsActive = true
                };

                _context.Items.Add(item);
                await _context.SaveChangesAsync();
            }

            var newProduct = new Domains.Entities.FinishedProduct
            {
                ItemId = item.ItemId,
                SellingPrice = request.SellingPrice,
                Sku = request.Sku,
                Variant = request.Variant,
                ImageUrl = request.ImageUrl ?? string.Empty
            };

            _context.FinishedProducts.Add(newProduct);
            await _context.SaveChangesAsync();

            var response = new FinishedProductResponse
            {
                ProductId = newProduct.ProductId,
                ItemId = newProduct.ItemId,
                SellingPrice = newProduct.SellingPrice,
                Sku = newProduct.Sku,
                ItemName = item.ItemName,
                Variant = newProduct.Variant,
                ImageUrl = newProduct.ImageUrl
            };

            return ApiResponse<FinishedProductResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating finished product.");
            return ApiResponse<FinishedProductResponse>.FailureResponse("An error occurred while creating finished product.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteFinishedProductAsync(int id)
    {
        try
        {
            var product = await _context.FinishedProducts.FindAsync(id);
            if (product == null)
            {
                return ApiResponse<bool>.FailureResponse("Finished product variant not found.");
            }

            _context.FinishedProducts.Remove(product);
            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting finished product.");
            return ApiResponse<bool>.FailureResponse("Cannot delete product variant because it may be referenced in recipes or production batches.");
        }
    }

    public async Task<ApiResponse<FinishedProductResponse>> UpdateFinishedProductAsync(int id, api_scm.Contracts.Requests.UpdateFinishedProductRequest request)
    {
        try
        {
            var product = await _context.FinishedProducts.Include(fp => fp.Item).FirstOrDefaultAsync(fp => fp.ProductId == id);
            if (product == null)
            {
                return ApiResponse<FinishedProductResponse>.FailureResponse("Finished product not found.");
            }

            product.Variant = request.Variant;
            product.SellingPrice = request.SellingPrice;
            product.Sku = request.Sku;
            if (request.ImageUrl != null)
            {
                product.ImageUrl = request.ImageUrl;
            }

            if (product.Item != null)
            {
                product.Item.ItemName = request.ProductName;
            }

            await _context.SaveChangesAsync();

            var response = new FinishedProductResponse
            {
                ProductId = product.ProductId,
                ItemId = product.ItemId,
                SellingPrice = product.SellingPrice,
                Sku = product.Sku,
                ItemName = product.Item?.ItemName ?? string.Empty,
                Variant = product.Variant,
                ImageUrl = product.ImageUrl ?? string.Empty
            };

            return ApiResponse<FinishedProductResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating finished product.");
            return ApiResponse<FinishedProductResponse>.FailureResponse("An error occurred while updating finished product.");
        }
    }
}
