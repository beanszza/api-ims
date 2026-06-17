namespace api_scm.Contracts.Requests;

public class UpdateStockTransferRequest
{
    public int ProductId { get; set; }
    public int SourceLocationId { get; set; }
    public int DestLocationId { get; set; }
    public int TransferQuantity { get; set; }
    public DateTime? TransferDate { get; set; }
}
