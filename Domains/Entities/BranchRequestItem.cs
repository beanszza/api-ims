namespace Domains.Entities;

public class BranchRequestItem
{
    public int BranchRequestItemId { get; set; }

    public int BranchRequestId { get; set; }
    public BranchRequest BranchRequest { get; set; } = null!;

    public int ProductId { get; set; }
    public FinishedProduct Product { get; set; } = null!;

    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }

    public string? Notes { get; set; }
}
