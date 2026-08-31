using Domains.Entities;
using Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructures.Persistence;

/// <summary>
/// Seeds the locations the system resolves by role.
/// </summary>
/// <remarks>
/// Idempotent, and matched on role rather than name so renaming "Main Warehouse" in the UI does not
/// cause a second one to be created on the next start. Only one system location exists per role.
/// </remarks>
public static class LocationSeeder
{
    private sealed record LocationSeed(LocationType Type, string Name, string Address);

    private static readonly LocationSeed[] Seeds =
    [
        new(LocationType.Warehouse, "Main Warehouse", "Commissary"),
        new(LocationType.Production, "Production Floor", "Commissary"),
        new(LocationType.Wip, "Work In Progress", "Commissary"),
        new(LocationType.Quarantine, "Quarantine Hold", "Commissary"),
        new(LocationType.FinishedGoods, "Finished Goods", "Commissary"),
        new(LocationType.Disposal, "Disposal / Write-off", "Commissary")
    ];

    public static void Seed(ScmDbContext db, ILogger? logger = null)
    {
        var existing = db.Locations.ToList();
        var added = 0;

        foreach (var seed in Seeds)
        {
            // Already have a system location for this role.
            if (existing.Any(l => l.IsSystemLocation && l.LocationType == seed.Type))
            {
                continue;
            }

            // Adopt a matching non-system location rather than creating a duplicate. This picks up the
            // "Finished Goods" and "Main Warehouse" locations earlier code created on the fly.
            var adoptable = existing.FirstOrDefault(l =>
                l.LocationType == seed.Type
                || string.Equals(l.LocationName, seed.Name, StringComparison.OrdinalIgnoreCase));

            if (adoptable is not null)
            {
                adoptable.LocationType = seed.Type;
                adoptable.IsSystemLocation = true;
                logger?.LogInformation(
                    "Adopted existing location '{Location}' as the system {Role} location.",
                    adoptable.LocationName, seed.Type);
                continue;
            }

            var created = new Location
            {
                LocationName = seed.Name,
                LocationType = seed.Type,
                Address = seed.Address,
                IsActive = true,
                Status = "Active",
                IsSystemLocation = true
            };
            db.Locations.Add(created);
            existing.Add(created);
            added++;
        }

        if (added > 0 || db.ChangeTracker.HasChanges())
        {
            db.SaveChanges();
            logger?.LogInformation("System locations seeded ({Added} created).", added);
        }
    }

    /// <summary>
    /// Gives every branch its own in-transit lane, so dispatched stock has somewhere to sit between
    /// leaving the commissary and being confirmed at the destination.
    /// </summary>
    /// <remarks>Used by the two-sided transfer work in Task 36.</remarks>
    public static void SeedInTransitLanesForBranches(ScmDbContext db, ILogger? logger = null)
    {
        var branches = db.Locations
            .Where(l => l.LocationType == LocationType.Branch || l.LocationType == LocationType.Bazaar)
            .ToList();

        if (branches.Count == 0)
        {
            return;
        }

        var existingLanes = db.Locations
            .Where(l => l.LocationType == LocationType.InTransit)
            .ToList();

        var created = 0;
        foreach (var branch in branches)
        {
            if (existingLanes.Any(l => l.ParentLocationId == branch.LocationId))
            {
                continue;
            }

            db.Locations.Add(new Location
            {
                LocationName = $"In Transit - {branch.LocationName}",
                LocationType = LocationType.InTransit,
                Address = branch.Address,
                IsActive = true,
                Status = "Active",
                IsSystemLocation = true,
                ParentLocationId = branch.LocationId
            });
            created++;
        }

        if (created > 0)
        {
            db.SaveChanges();
            logger?.LogInformation("Created {Count} in-transit lane(s) for branches.", created);
        }
    }
}
