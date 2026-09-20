using System.Threading.Tasks;
using System.Collections.Generic;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ILotService
{
    /// <summary>
    /// Paginated flat list of all lots, with optional filters. Ordered by ExpiryDate ASC (FEFO).
    /// </summary>
    Task<ApiResponse<PagedData<LotResponse>>> GetAllLotsAsync(
        string? itemName = null,
        string? locationName = null,
        string? status = null,
        int page = 1,
        int pageSize = 10);

    /// <summary>
    /// All lots for a specific item, sorted FEFO (ExpiryDate ASC, then ReceivedDate ASC for non-expiring).
    /// Each row includes SharePercent = QuantityRemaining / totalAvailable × 100.
    /// The first row with Status=Available is the FEFO-consume-first lot.
    /// </summary>
    Task<ApiResponse<List<LotResponse>>> GetLotsByItemAsync(int itemId);
}