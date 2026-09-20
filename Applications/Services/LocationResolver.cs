using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

/// <inheritdoc />
public sealed class LocationResolver : ILocationResolver
{
    /// <summary>Location roles goods may legitimately be received into.</summary>
    private static readonly LocationType[] ValidReceivingTypes =
        [LocationType.Warehouse, LocationType.Quarantine];

    private readonly ScmDbContext _context;

    public LocationResolver(ScmDbContext context) => _context = context;

    public async Task<Location> RequireSystemLocationAsync(LocationType locationType)
    {
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.IsSystemLocation && l.LocationType == locationType);

        return location ?? throw new InvalidOperationException(
            $"No system location is configured for {EnumDbValue.ToDbValue(locationType)}. " +
            "System locations are seeded at startup; if this is missing, the seeder did not run.");
    }

    public async Task<Location> ResolveReceivingLocationAsync(int? requestedLocationId)
    {
        if (requestedLocationId is null)
        {
            // No explicit choice: use the designated receiving warehouse. Note the difference from the
            // old behaviour - this is a location chosen for the role, not whichever row sorted first.
            return await RequireSystemLocationAsync(LocationType.Warehouse);
        }

        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.LocationId == requestedLocationId.Value)
            ?? throw new InvalidOperationException(
                $"Receiving location {requestedLocationId} does not exist.");

        if (!location.IsActive)
        {
            throw new InvalidOperationException(
                $"'{location.LocationName}' is not active and cannot receive stock.");
        }

        if (!ValidReceivingTypes.Contains(location.LocationType))
        {
            var allowed = string.Join(" or ", ValidReceivingTypes.Select(EnumDbValue.ToDbValue));
            throw new InvalidOperationException(
                $"'{location.LocationName}' is a {EnumDbValue.ToDbValue(location.LocationType)} " +
                $"location. Goods can only be received into a {allowed} location.");
        }

        return location;
    }
}
