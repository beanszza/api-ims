using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinishedProductsController : ControllerBase
{
    private readonly IFinishedProductService _finishedProductService;

    public FinishedProductsController(IFinishedProductService finishedProductService)
    {
        _finishedProductService = finishedProductService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<FinishedProductResponse>>>> GetAllFinishedProducts()
    {
        var result = await _finishedProductService.GetAllFinishedProductsAsync();
        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}
