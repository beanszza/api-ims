using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CompletePutAwayRequest
{
    [Required]
    public int DestinationLocationId { get; set; }

    public string? LotCode { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? SerialNumber { get; set; }

    public string? Notes { get; set; }
}

public class BatchCompletePutAwayRequest
{
    [Required]
    public List<BatchPutAwayItemRequest> Items { get; set; } = new();
}

public class BatchPutAwayItemRequest
{
    [Required]
    public int PutAwayId { get; set; }

    [Required]
    public int DestinationLocationId { get; set; }

    public string? LotCode { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? SerialNumber { get; set; }

    public string? Notes { get; set; }
}
