using System.Threading.Tasks;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ITraceabilityService
{
    /// <summary>
    /// Forward trace: From raw material/packaging lot -> Production batches -> Finished Goods lots -> Retail branch shipments.
    /// </summary>
    Task<ApiResponse<ForwardTraceResponse>> TraceForwardAsync(string lotCode);

    /// <summary>
    /// Backward trace: From finished goods lot -> Production batch -> Consumed ingredient/packaging lots -> Supplier origins.
    /// </summary>
    Task<ApiResponse<BackwardTraceResponse>> TraceBackwardAsync(string fgLotCode);
}
