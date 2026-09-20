namespace api_scm.Contracts.Requests;

public class UpdateStockTransferStatusRequest
{
    // E.g., "In Transit", "Completed", "Cancelled"
    public string Status { get; set; } = string.Empty;
}
