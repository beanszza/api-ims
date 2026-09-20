using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Microsoft.AspNetCore.Http;

namespace Applications.Interfaces;

public interface IPurchaseOrderService
{
    Task<ApiResponse<PurchaseOrderResponse>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request);
    Task<ApiResponse<PagedData<PurchaseOrderResponse>>> GetPurchaseOrdersAsync(string? status = null, string? search = null, int page = 1, int pageSize = 10);
    Task<ApiResponse<PurchaseOrderResponse>> GetPurchaseOrderByIdAsync(int id);
    Task<ApiResponse<PurchaseOrderResponse>> UpdateOrderStatusAsync(int id, UpdatePurchaseOrderQaRequest request);
    Task<ApiResponse<PurchaseOrderResponse>> UpdatePurchaseOrderAsync(int id, CreatePurchaseOrderRequest request);
    Task<ApiResponse<PurchaseOrderResponse>> UploadReceiptAsync(int id, IFormFile file);
    Task<ApiResponse<PurchaseOrderResponse>> DeleteReceiptAsync(int id);
    Task<ApiResponse<PagedData<TransactionHistoryResponse>>> GetTransactionHistoryAsync(string? filterType = null, DateTime? specificDate = null, int page = 1, int pageSize = 10);
    Task<string> ExportTransactionHistoryCsvAsync(string? filterType = null, DateTime? specificDate = null);

    /// <summary>
    /// Returns the sum of PoItemQuantity per item across all non-cancelled POs referencing <paramref name="prId"/>.
    /// Used by the frontend to enforce per-item ordering limits.
    /// </summary>
    Task<ApiResponse<List<PrItemOrderedQtyResponse>>> GetOrderedQtyForPrAsync(int prId);

    /// <summary>
    /// Returns a preview of the next sequential PO number that will be assigned.
    /// </summary>
    Task<ApiResponse<string>> GetNextPoNumberPreviewAsync();
}
