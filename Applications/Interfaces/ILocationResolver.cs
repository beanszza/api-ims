using Domains.Entities;
using Domains.Enums;

namespace Applications.Interfaces;

/// <summary>
/// Finds the location that plays a given role.
/// </summary>
/// <remarks>
/// Replaces the pattern of guessing: receiving used <c>Locations.FirstOrDefaultAsync()</c> - literally
/// whichever row came back first - and posting finished goods matched on a name containing "finished".
/// Both silently created a location when none was found, so a typo produced a second warehouse rather
/// than an error.
/// </remarks>
public interface ILocationResolver
{
    /// <summary>
    /// The system location for a role.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No system location is configured for that role. Thrown rather than creating one, because
    /// inventing a location mid-posting is how duplicate warehouses appeared.
    /// </exception>
    Task<Location> RequireSystemLocationAsync(LocationType locationType);

    /// <summary>
    /// Validates a caller-supplied location for receiving, or falls back to the system receiving
    /// warehouse when none was supplied.
    /// </summary>
    Task<Location> ResolveReceivingLocationAsync(int? requestedLocationId);
}
