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
    Task<ApiResponse<IEnumerable<PurchaseOrderResponse>>> GetPurchaseOrdersAsync(string? status = null);
    Task<ApiResponse<PurchaseOrderResponse>> UpdateOrderStatusAsync(int id, string status);
    Task<ApiResponse<PurchaseOrderResponse>> UpdatePurchaseOrderAsync(int id, CreatePurchaseOrderRequest request);
    Task<ApiResponse<PurchaseOrderResponse>> UploadReceiptAsync(int id, IFormFile file);
    Task<ApiResponse<IEnumerable<TransactionHistoryResponse>>> GetTransactionHistoryAsync(string? filterType = null, DateTime? specificDate = null);
    Task<string> ExportTransactionHistoryCsvAsync(string? filterType = null, DateTime? specificDate = null);
}
