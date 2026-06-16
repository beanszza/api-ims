using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Applications.Interfaces;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<SupplierResponse>>>> GetAllSuppliers([FromQuery] string? supplierName = null, [FromQuery] bool? isActive = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _supplierService.GetAllSuppliersAsync(supplierName, isActive, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> GetSupplierById([FromRoute] int id)
    {
        var result = await _supplierService.GetSupplierByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> CreateSupplier([FromBody] CreateSupplierRequest request)
    {
        var result = await _supplierService.CreateSupplierAsync(request);
        return result.Success
            ? CreatedAtAction(nameof(GetSupplierById), new { id = result.Data?.SupplierId }, result)
            : StatusCode(400, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> UpdateSupplier([FromRoute] int id, [FromBody] UpdateSupplierRequest request)
    {
        var result = await _supplierService.UpdateSupplierAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<EmptyPayload>>> DeleteSupplier([FromRoute] int id)
    {
        var result = await _supplierService.DeleteSupplierAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}