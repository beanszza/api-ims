using Domains.Entities;
using Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructures.Persistence;

/// <summary>
/// Seeds the standard units of measure and keeps their dimension and conversion factor correct.
/// </summary>
/// <remarks>
/// Idempotent: safe to run on every start. Existing rows are matched by code, then by abbreviation so
/// units created before codes existed are adopted rather than duplicated, and their conversion data
/// is filled in.
/// </remarks>
public static class UnitOfMeasureSeeder
{
    /// <summary>Codes referenced from code and tests, so the strings live in one place.</summary>
    public static class Codes
    {
        public const string Kilogram = "kg";
        public const string Gram = "g";
        public const string Sack50Kg = "sack50";
        public const string Litre = "L";
        public const string Millilitre = "mL";
        public const string Piece = "pcs";
        public const string Box = "box";
        public const string Pack = "pack";
        public const string Roll = "roll";
        public const string Bottle = "bottle";
        public const string Metre = "m";
    }

    private sealed record UomSeed(
        string Code,
        string Name,
        string Abbreviation,
        UomType Type,
        decimal Factor,
        bool IsBase);

    private static readonly UomSeed[] Seeds =
    [
        // Weight, base kilogram.
        new(Codes.Kilogram, "Kilogram", "kg", UomType.Weight, 1m, true),
        new(Codes.Gram, "Gram", "g", UomType.Weight, 0.001m, false),
        // A sack is a weight unit, not a container: one sack is 50 kg of whatever it holds.
        new(Codes.Sack50Kg, "Sack (50 kg)", "sack", UomType.Weight, 50m, false),

        // Volume, base litre.
        new(Codes.Litre, "Litre", "L", UomType.Volume, 1m, true),
        new(Codes.Millilitre, "Millilitre", "mL", UomType.Volume, 0.001m, false),

        // Count, base piece. Container-style units stay at 1: how many pieces are in a box depends on
        // the supplier's packaging, which lives on SupplierItem.PackSize (Task 12), not on the unit.
        new(Codes.Piece, "Piece", "pcs", UomType.Count, 1m, true),
        new(Codes.Box, "Box", "box", UomType.Count, 1m, false),
        new(Codes.Pack, "Pack", "pack", UomType.Count, 1m, false),
        new(Codes.Roll, "Roll", "roll", UomType.Count, 1m, false),
        new(Codes.Bottle, "Bottle", "bottle", UomType.Count, 1m, false),

        // Length, base metre.
        new(Codes.Metre, "Meter", "m", UomType.Length, 1m, true)
    ];

    public static void Seed(ScmDbContext db, ILogger? logger = null)
    {
        var existing = db.UnitOfMeasures.ToList();
        var changed = false;

        // Pass one: make sure every unit exists and carries the right dimension and factor.
        foreach (var seed in Seeds)
        {
            var row = existing.FirstOrDefault(u =>
                          string.Equals(u.Code, seed.Code, StringComparison.OrdinalIgnoreCase))
                      ?? existing.FirstOrDefault(u =>
                          string.Equals(u.Abbreviation, seed.Abbreviation, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                row = new UnitOfMeasure { Name = seed.Name, Abbreviation = seed.Abbreviation };
                db.UnitOfMeasures.Add(row);
                existing.Add(row);
                changed = true;
            }

            if (row.Code != seed.Code) { row.Code = seed.Code; changed = true; }
            if (row.UomType != seed.Type) { row.UomType = seed.Type; changed = true; }
            if (row.ConversionFactor != seed.Factor) { row.ConversionFactor = seed.Factor; changed = true; }
            if (row.IsBaseUnit != seed.IsBase) { row.IsBaseUnit = seed.IsBase; changed = true; }
        }

        if (changed)
        {
            db.SaveChanges();
        }

        // Pass two: point every non-base unit at its dimension's base. Needs a second pass because
        // base units only have ids once they are saved.
        var all = db.UnitOfMeasures.ToList();
        var bases = all.Where(u => u.IsBaseUnit).ToDictionary(u => u.UomType, u => u.UomId);
        var linked = false;

        foreach (var uom in all)
        {
            // Base units are their own reference point, so they carry no BaseUomId.
            var expected = uom.IsBaseUnit
                ? (int?)null
                : bases.TryGetValue(uom.UomType, out var baseId) ? baseId : null;

            if (uom.BaseUomId != expected)
            {
                uom.BaseUomId = expected;
                linked = true;
            }
        }

        if (linked)
        {
            db.SaveChanges();
        }

        // Any unit that predates this seeder and is not in the list above still needs a usable
        // factor, otherwise conversion would divide by zero.
        var repaired = db.UnitOfMeasures.Where(u => u.ConversionFactor <= 0).ToList();
        foreach (var uom in repaired)
        {
            uom.ConversionFactor = 1m;
            logger?.LogWarning(
                "Unit of measure '{Uom}' had a non-positive conversion factor; defaulted to 1. " +
                "Set its dimension and factor explicitly if it is not an each-style unit.",
                uom.Abbreviation);
        }

        if (repaired.Count > 0)
        {
            db.SaveChanges();
        }

        if (changed || linked || repaired.Count > 0)
        {
            logger?.LogInformation("Unit of Measures seeded with conversion data.");
        }
    }

    /// <summary>
    /// Ensures every item has a stocking unit. Items created before <c>StockUomId</c> existed inherit
    /// their display unit, which is what the old code effectively assumed.
    /// </summary>
    public static void BackfillItemStockUom(ScmDbContext db, ILogger? logger = null)
    {
        var orphans = db.Items.Where(i => i.StockUomId == 0).ToList();
        if (orphans.Count == 0)
        {
            return;
        }

        foreach (var item in orphans)
        {
            item.StockUomId = item.UomId;
        }

        db.SaveChanges();
        logger?.LogInformation(
            "Backfilled the stocking unit of measure for {Count} item(s) from their display unit.",
            orphans.Count);
    }
}
