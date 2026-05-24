using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IItemService
{
    Task<ApiResponse<IEnumerable<ItemResponse>>> GetAllItemsAsync(string? search = null, string? category = null, bool? isActive = null, string? sort = "asc");
    Task<ApiResponse<ItemResponse>> GetItemByIdAsync(int id);
	Task<ApiResponse<ItemResponse>> CreateItemAsync(CreateItemRequest request);
	Task<ApiResponse<ItemResponse>> UpdateItemAsync(int id, UpdateItemRequest request);
	Task<ApiResponse<EmptyPayload>> DeleteItemAsync(int id);
}