using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IInventoryService
{
    Task<ApiResponse<IEnumerable<InventoryResponse>>> GetAllInventoriesAsync();
}
