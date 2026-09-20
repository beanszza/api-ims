using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IGoodsReceiptService
{
    Task<ApiResponse<List<GoodsReceiptResponse>>> GetGoodsReceiptsAsync(int? poId = null);
    Task<ApiResponse<GoodsReceiptResponse>> GetGoodsReceiptByIdAsync(int grnId);
    Task<ApiResponse<GoodsReceiptResponse>> ReceiveGoodsAsync(CreateGoodsReceiptRequest request);
}
