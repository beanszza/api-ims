using System;

namespace Domains.Entities;

/// <summary>
/// Authoritative catalog link between a supplier and an item.
/// Defines pricing, purchasing packaging/UOM, minimum order quantities, lead time, and preference.
/// </summary>
public class SupplierItem
{
    public int SupplierId { get; set; }
    public int ItemId { get; set; }

    /// <summary>Supplier's internal product / catalog SKU code.</summary>
    public string? SupplierSku { get; set; }

    /// <summary>Supplier's trade or brand name for this item.</summary>
    public string? SupplierItemName { get; set; }

    /// <summary>Current agreed purchase price per purchasing unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Currency code (e.g. PHP).</summary>
    public string Currency { get; set; } = "PHP";

    /// <summary>Unit of measure the supplier sells this in (e.g., Sacks, Boxes, Buckets, kg).</summary>
    public int PurchaseUomId { get; set; }

    /// <summary>
    /// How many stocking units (Item.StockUomId) are in one purchasing unit.
    /// E.g., if StockUom is kg and PurchaseUom is a 50kg Sack, PackSize is 50.000.
    /// </summary>
    public decimal PackSize { get; set; } = 1m;

    /// <summary>Expected delivery lead time in calendar days from order confirmation.</summary>
    public int LeadTimeDays { get; set; } = 3;

    /// <summary>Minimum quantity supplier accepts per order, denominated in PurchaseUomId.</summary>
    public decimal MinOrderQuantity { get; set; } = 1m;

    /// <summary>Whether this supplier is the primary / preferred vendor for this item.</summary>
    public bool IsPreferred { get; set; }

    /// <summary>Historical record of the last actual invoiced price.</summary>
    public decimal? LastPurchasePrice { get; set; }

    /// <summary>Date of the last completed purchase order from this supplier for this item.</summary>
    public DateTime? LastPurchaseDate { get; set; }

    /// <summary>Active status flag.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public Supplier? Supplier { get; set; }
    public Item? Item { get; set; }
    public UnitOfMeasure? PurchaseUom { get; set; }
}
