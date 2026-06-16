namespace api_scm.Contracts.Responses;

public class TransferDashboardResponse
{
    public int PendingCount { get; set; }
    public int InTransitCount { get; set; }
    public int CompletedCount { get; set; }
}
