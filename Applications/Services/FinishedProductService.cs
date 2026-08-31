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
                    Variant = fp.Variant
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
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryName.ToLower().Contains("finished good"));
            if (category == null)
            {
                category = new Domains.Entities.Category { CategoryName = "Finished Good", Description = "Finished Goods" };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Abbreviation == "pcs") 
                      ?? await _context.UnitOfMeasures.FirstOrDefaultAsync();

            var newItem = new Domains.Entities.Item
            {
                ItemName = request.ProductName,
                UomId = uom?.UomId ?? 1,
                StockUomId = uom?.UomId ?? 1,
                CategoryId = category?.CategoryId ?? 1,
                MinStockLevel = 0,
                MaxStockLevel = 100,
                IsActive = true
            };

            _context.Items.Add(newItem);
            await _context.SaveChangesAsync();

            var newProduct = new Domains.Entities.FinishedProduct
            {
                ItemId = newItem.ItemId,
                SellingPrice = 0,
                Sku = "",
                Variant = request.Variant
            };

            _context.FinishedProducts.Add(newProduct);
            await _context.SaveChangesAsync();

            var response = new FinishedProductResponse
            {
                ProductId = newProduct.ProductId,
                ItemId = newProduct.ItemId,
                SellingPrice = newProduct.SellingPrice,
                Sku = newProduct.Sku,
                ItemName = newItem.ItemName,
                Variant = newProduct.Variant
            };

            return ApiResponse<FinishedProductResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating finished product.");
            return ApiResponse<FinishedProductResponse>.FailureResponse("An error occurred while creating finished product.");
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
                Variant = product.Variant
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
