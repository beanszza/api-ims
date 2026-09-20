using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ISupplierItemService
{
    /// <summary>
    /// Gets all catalog entries for a supplier.
    /// </summary>
    Task<ApiResponse<List<SupplierItemResponse>>> GetItemsBySupplierAsync(int supplierId);

    /// <summary>
    /// Gets all suppliers providing a specific item, ordered with preferred suppliers first.
    /// Used by the PO creation UI to narrow the supplier dropdown to only valid vendors.
    /// </summary>
    Task<ApiResponse<List<ItemSupplierOptionResponse>>> GetSuppliersByItemAsync(int itemId);

    /// <summary>
    /// Gets a single supplier-item catalog entry.
    /// </summary>
    Task<ApiResponse<SupplierItemResponse>> GetSupplierItemAsync(int supplierId, int itemId);

    /// <summary>
    /// Adds or updates a supplier-item link in the catalog.
    /// </summary>
    Task<ApiResponse<SupplierItemResponse>> UpsertSupplierItemAsync(CreateOrUpdateSupplierItemRequest request);

    /// <summary>
    /// Deactivates or removes an item from a supplier's catalog.
    /// </summary>
    Task<ApiResponse<bool>> RemoveSupplierItemAsync(int supplierId, int itemId);
}
