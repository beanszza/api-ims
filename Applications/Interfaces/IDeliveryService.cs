using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IDeliveryService
{
    Task<ApiResponse<DeliveryResponse>> CreateDeliveryAsync(CreateDeliveryRequest request, CancellationToken ct = default);
    Task<ApiResponse<PagedData<DeliveryResponse>>> GetDeliveriesAsync(int? poId = null, string? status = null, int page = 1, int pageSize = 50, bool? eligibleForGrn = null, CancellationToken ct = default);
    Task<ApiResponse<DeliveryResponse>> GetDeliveryByIdAsync(int deliveryId, CancellationToken ct = default);
    Task<ApiResponse<DeliveryResponse>> MarkDispatchedAsync(int deliveryId, MarkDispatchedRequest request, CancellationToken ct = default);
    Task<ApiResponse<DeliveryResponse>> MarkArrivedAsync(int deliveryId, MarkArrivedRequest request, CancellationToken ct = default);
    Task<ApiResponse<DeliveryResponse>> CancelDeliveryAsync(int deliveryId, string? reason = null, CancellationToken ct = default);
    Task<ApiResponse<List<DeliveryItemResponse>>> GetOutstandingPoItemsAsync(int poId, CancellationToken ct = default);
}
