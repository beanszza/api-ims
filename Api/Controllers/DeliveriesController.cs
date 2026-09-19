using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveriesController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;

    public DeliveriesController(IDeliveryService deliveryService)
    {
        _deliveryService = deliveryService;
    }

    /// <summary>Lists delivery shipments with optional filtering by PO and Status.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<DeliveryResponse>>>> GetAll(
        [FromQuery] int? poId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _deliveryService.GetDeliveriesAsync(poId, status, page, pageSize, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific delivery shipment by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DeliveryResponse>>> GetById(int id, CancellationToken ct = default)
    {
        var result = await _deliveryService.GetDeliveryByIdAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Returns remaining outstanding items for a PO considering existing scheduled/in-transit deliveries.</summary>
    [HttpGet("po/{poId:int}/outstanding")]
    public async Task<ActionResult<ApiResponse<List<DeliveryItemResponse>>>> GetOutstandingPoItems(int poId, CancellationToken ct = default)
    {
        var result = await _deliveryService.GetOutstandingPoItemsAsync(poId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Schedules a new delivery shipment against an approved or ordered PO.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DeliveryResponse>>> Create(
        [FromBody] CreateDeliveryRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _deliveryService.CreateDeliveryAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Marks a scheduled delivery as dispatched / in-transit.</summary>
    [HttpPut("{id:int}/dispatch")]
    public async Task<ActionResult<ApiResponse<DeliveryResponse>>> MarkDispatched(
        int id,
        [FromBody] MarkDispatchedRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _deliveryService.MarkDispatchedAsync(id, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Marks an in-transit delivery as arrived at our receiving location.</summary>
    [HttpPut("{id:int}/arrive")]
    public async Task<ActionResult<ApiResponse<DeliveryResponse>>> MarkArrived(
        int id,
        [FromBody] MarkArrivedRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _deliveryService.MarkArrivedAsync(id, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Cancels a delivery shipment before it arrives.</summary>
    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<DeliveryResponse>>> Cancel(
        int id,
        [FromBody] CancelDeliveryRequest? request,
        CancellationToken ct = default)
    {
        var result = await _deliveryService.CancelDeliveryAsync(id, request?.Reason, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
