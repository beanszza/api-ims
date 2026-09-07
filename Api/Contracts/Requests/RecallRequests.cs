using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class SimulateRecallRequest
{
    [Required]
    public string LotCode { get; set; } = string.Empty;

    [Required]
    public string Reason { get; set; } = "Contamination / Quality Defect";

    /// <summary>
    /// If true, automatically cascades and sets all downstream affected finished goods lots to Quarantine status.
    /// </summary>
    public bool ApplyQuarantineHold { get; set; } = false;

    public string? Notes { get; set; }
}
