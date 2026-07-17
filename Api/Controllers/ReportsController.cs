using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Api.Contracts.Requests;
using api_scm.Api.Contracts.Responses;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<IEnumerable<InventoryReportDto>>>> GetInventoryReport([FromQuery] ReportFilterDto filter)
    {
        var result = await _reportService.GetInventoryReportAsync(filter);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }

    [HttpGet("procurement")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProcurementReportDto>>>> GetProcurementReport([FromQuery] ReportFilterDto filter)
    {
        var result = await _reportService.GetProcurementReportAsync(filter);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }

    [HttpGet("production")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProductionQualityReportDto>>>> GetProductionReport([FromQuery] ReportFilterDto filter)
    {
        var result = await _reportService.GetProductionReportAsync(filter);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }

    [HttpGet("supplier")]
    public async Task<ActionResult<ApiResponse<IEnumerable<SupplierPerformanceReportDto>>>> GetSupplierReport([FromQuery] ReportFilterDto filter)
    {
        var result = await _reportService.GetSupplierReportAsync(filter);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }

    [HttpGet("distribution")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DistributionReportDto>>>> GetDistributionReport([FromQuery] ReportFilterDto filter)
    {
        var result = await _reportService.GetDistributionReportAsync(filter);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }
}
