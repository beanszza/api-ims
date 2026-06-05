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
                    ItemName = fp.Item != null ? fp.Item.ItemName : string.Empty
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
}
