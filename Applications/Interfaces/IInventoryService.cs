using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IInventoryService
{
    Task<ApiResponse<PagedData<InventoryResponse>>> GetAllInventoriesAsync(string? categoryName = null, int page = 1, int pageSize = 10);
}
