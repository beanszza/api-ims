using System;

namespace Domains.Entities;

public class StockInLine
{
    public int StockInLineId { get; set; }

    public int StockInId { get; set; }
    public StockIn StockIn { get; set; } = null!;

    public int? GrnItemId { get; set; }
    public GoodsReceiptItem? GrnItem { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int? PurchaseUomId { get; set; }
    public UnitOfMeasure? PurchaseUom { get; set; }

    public decimal QuantityToStock { get; set; }

    public decimal CurrentStockBeforeCommit { get; set; }

    /// <summary>System-generated batch lot number, e.g. "LOT-20260923-101-01".</summary>
    public string LotCode { get; set; } = string.Empty;

    public DateTime? ExpiryDate { get; set; }

    public bool CommittedToInventory { get; set; }

    public int? InventoryLotId { get; set; }
    public InventoryLot? InventoryLot { get; set; }

    public string? Notes { get; set; }
}
