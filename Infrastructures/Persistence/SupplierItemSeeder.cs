using System;
using System.Linq;
using Domains.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructures.Persistence;

public static class SupplierItemSeeder
{
    public static void Seed(ScmDbContext db, ILogger logger)
    {
        if (db.SupplierItems.Any())
            return;

        var suppliers = db.Suppliers.ToList();
        var items = db.Items.Include(i => i.Category).ToList();
        var uoms = db.UnitOfMeasures.ToList();

        if (!suppliers.Any() || !items.Any())
            return;

        logger.LogInformation("→ Seeding Supplier-Item catalog entries...");

        // Find standard UOMs
        var kgUom = uoms.FirstOrDefault(u => u.Code == "kg")?.UomId ?? items.First().StockUomId;
        var pcsUom = uoms.FirstOrDefault(u => u.Code == "pcs")?.UomId ?? items.First().StockUomId;
        var sack50kg = uoms.FirstOrDefault(u => u.Code == "sack_50kg")?.UomId ?? kgUom;

        var entries = new List<SupplierItem>();

        foreach (var item in items)
        {
            var categoryName = item.Category?.CategoryName?.ToLower() ?? "";
            if (categoryName.Contains("finished"))
                continue; // Do not catalogue finished goods to external suppliers

            var itemName = item.ItemName.ToLower();

            // Match suppliers intelligently based on company name or item type
            foreach (var supplier in suppliers)
            {
                var sName = supplier.CompanyName.ToLower();

                bool isMatch = false;
                decimal unitPrice = 100m;
                int purchaseUomId = item.StockUomId;
                decimal packSize = 1m;
                int leadTime = 3;
                bool isPreferred = false;

                if (itemName.Contains("ube") || itemName.Contains("yam") || itemName.Contains("taro"))
                {
                    if (sName.Contains("agri") || sName.Contains("farm") || sName.Contains("harvest") || sName.Contains("produce") || sName.Contains("fresh") || sName.Contains("supplier a"))
                    {
                        isMatch = true;
                        unitPrice = 85.50m; // Price per kg
                        purchaseUomId = sack50kg;
                        packSize = 50m;
                        leadTime = 2;
                        isPreferred = sName.Contains("agri") || sName.Contains("supplier a");
                    }
                }
                else if (itemName.Contains("sugar") || itemName.Contains("sweet"))
                {
                    if (sName.Contains("sugar") || sName.Contains("sweet") || sName.Contains("commodit") || sName.Contains("supplier b") || sName.Contains("trade"))
                    {
                        isMatch = true;
                        unitPrice = 65.00m;
                        purchaseUomId = sack50kg;
                        packSize = 50m;
                        leadTime = 3;
                        isPreferred = sName.Contains("sugar") || sName.Contains("supplier b");
                    }
                }
                else if (itemName.Contains("milk") || itemName.Contains("butter") || itemName.Contains("dairy"))
                {
                    if (sName.Contains("dairy") || sName.Contains("milk") || sName.Contains("food") || sName.Contains("supplier c"))
                    {
                        isMatch = true;
                        unitPrice = 48.00m;
                        purchaseUomId = item.StockUomId;
                        packSize = 1m;
                        leadTime = 2;
                        isPreferred = sName.Contains("dairy") || sName.Contains("supplier c");
                    }
                }
                else if (itemName.Contains("jar") || itemName.Contains("lid") || itemName.Contains("bottle") || itemName.Contains("label") || itemName.Contains("packaging") || itemName.Contains("box") || itemName.Contains("carton"))
                {
                    if (sName.Contains("pack") || sName.Contains("glass") || sName.Contains("container") || sName.Contains("print") || sName.Contains("supplier"))
                    {
                        isMatch = true;
                        unitPrice = itemName.Contains("jar") ? 14.50m : itemName.Contains("lid") ? 3.20m : 2.50m;
                        purchaseUomId = pcsUom;
                        packSize = itemName.Contains("jar") ? 24m : 100m;
                        leadTime = 5;
                        isPreferred = sName.Contains("pack") || sName.Contains("glass");
                    }
                }
                else
                {
                    // General fallback supplier assignment
                    isMatch = true;
                    unitPrice = 50.00m;
                    purchaseUomId = item.StockUomId;
                    packSize = 1m;
                    leadTime = 3;
                }

                if (isMatch)
                {
                    entries.Add(new SupplierItem
                    {
                        SupplierId = supplier.SupplierId,
                        ItemId = item.ItemId,
                        SupplierSku = $"{supplier.CompanyName[..Math.Min(3, supplier.CompanyName.Length)].ToUpper()}-{item.ItemId:D3}",
                        SupplierItemName = item.ItemName,
                        UnitPrice = unitPrice,
                        Currency = "PHP",
                        PurchaseUomId = purchaseUomId,
                        PackSize = packSize,
                        LeadTimeDays = leadTime,
                        MinOrderQuantity = 1m,
                        IsPreferred = isPreferred,
                        IsActive = true
                    });
                }
            }
        }

        if (entries.Any())
        {
            // Ensure only one preferred supplier per item
            var grouped = entries.GroupBy(e => e.ItemId);
            foreach (var group in grouped)
            {
                var preferredCount = group.Count(g => g.IsPreferred);
                if (preferredCount == 0)
                {
                    group.First().IsPreferred = true;
                }
                else if (preferredCount > 1)
                {
                    bool first = true;
                    foreach (var g in group.Where(x => x.IsPreferred))
                    {
                        if (!first) g.IsPreferred = false;
                        first = false;
                    }
                }
            }

            db.SupplierItems.AddRange(entries);
            db.SaveChanges();
            logger.LogInformation("✓ Seeded {Count} supplier-item catalog relations.", entries.Count);
        }
    }
}
