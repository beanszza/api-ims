using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IGoodsReceiptService
{
    Task<ApiResponse<List<GoodsReceiptResponse>>> GetGoodsReceiptsAsync(int? poId = null, int? deliveryId = null, string? status = null);
    Task<ApiResponse<GoodsReceiptResponse>> GetGoodsReceiptByIdAsync(int grnId);
    Task<ApiResponse<GoodsReceiptResponse>> CreateDraftGrnAsync(CreateGoodsReceiptRequest request);
    Task<ApiResponse<GoodsReceiptResponse>> UpdateDraftGrnAsync(int grnId, UpdateGoodsReceiptRequest request);
    Task<ApiResponse<GoodsReceiptResponse>> PostGrnAsync(int grnId);
    Task<ApiResponse<GoodsReceiptResponse>> CancelGrnAsync(int grnId);
    Task<ApiResponse<GoodsReceiptResponse>> ReceiveGoodsAsync(CreateGoodsReceiptRequest request);
}
