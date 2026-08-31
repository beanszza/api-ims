using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class LocationService : ILocationService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<LocationService> _logger;
    private readonly IAuditTrail _audit;

    public LocationService(
        ScmDbContext context,
        ILogger<LocationService> logger,
        IAuditTrail audit)
    {
        _context = context;
        _logger = logger;
        _audit = audit;
    }

    private static LocationResponse MapToResponse(Location l) => new()
    {
        LocationId = l.LocationId,
        LocationName = l.LocationName,
        LocationType = EnumDbValue.ToDbValue(l.LocationType),
        Address = l.Address,
        IsActive = l.IsActive,
        Status = l.Status,
        IsSystemLocation = l.IsSystemLocation,
        ParentLocationId = l.ParentLocationId
    };

    /// <summary>
    /// Parses a caller-supplied location type, returning an error message when it is not a known role.
    /// </summary>
    private static string? TryParseLocationType(string? raw, out LocationType parsed)
    {
        if (EnumDbValue.TryParse(raw, out parsed) && parsed != LocationType.Unspecified)
        {
            return null;
        }

        return $"'{raw}' is not a valid location type. " +
               $"Accepted values: {EnumDbValue.DescribeAccepted<LocationType>()}.";
    }

    public async Task<ApiResponse<PagedData<LocationResponse>>> GetAllLocationsAsync(int page = 1, int pageSize = 10)
    {
        try
        {
            var totalCount = await _context.Locations.CountAsync();

            // Materialise before mapping: LocationType is an enum on the entity and a string on the
            // response, and EnumDbValue has no SQL translation.
            var rows = await _context.Locations
                .AsNoTracking()
                .OrderBy(l => l.LocationId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var locations = rows.Select(MapToResponse).ToList();

            var pagedData = new PagedData<LocationResponse>
            {
                Items = locations,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<LocationResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations.");
            return ApiResponse<PagedData<LocationResponse>>.FailureResponse("An error occurred while fetching locations.");
        }
    }

    public async Task<ApiResponse<LocationResponse>> UpdateLocationAsync(int id, UpdateLocationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LocationName))
                return ApiResponse<LocationResponse>.FailureResponse("LocationName is required.");
            if (string.IsNullOrWhiteSpace(request.LocationType))
                return ApiResponse<LocationResponse>.FailureResponse("LocationType is required.");

            var typeError = TryParseLocationType(request.LocationType, out var requestedType);
            if (typeError is not null)
                return ApiResponse<LocationResponse>.FailureResponse(typeError);

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null)
                return ApiResponse<LocationResponse>.FailureResponse("Location not found.");

            // A system location is resolved by role, so changing its role would silently break posting.
            if (location.IsSystemLocation && location.LocationType != requestedType)
            {
                return ApiResponse<LocationResponse>.FailureResponse(
                    $"'{location.LocationName}' is a system location for " +
                    $"{EnumDbValue.ToDbValue(location.LocationType)} and its type cannot be changed. " +
                    "Rename it or add a separate location instead.");
            }

            // Field-level audit entries, attributed to whoever is making the request.
            var locationId = location.LocationId.ToString();

            if (location.LocationName != request.LocationName)
            {
                _audit.Record(nameof(Location), locationId, "Updated",
                    nameof(location.LocationName), location.LocationName, request.LocationName);
            }

            if (location.LocationType != requestedType)
            {
                _audit.Record(nameof(Location), locationId, "Updated",
                    nameof(location.LocationType),
                    EnumDbValue.ToDbValue(location.LocationType),
                    EnumDbValue.ToDbValue(requestedType));
            }

            var newStatus = string.IsNullOrWhiteSpace(request.Status) ? location.Status : request.Status;
            if (location.Status != newStatus)
            {
                _audit.Record(nameof(Location), locationId, "StatusUpdated",
                    nameof(location.Status), location.Status ?? "Active", newStatus);
            }

            location.LocationName = request.LocationName;
            location.LocationType = requestedType;
            location.Address = request.Address;
            location.IsActive = request.IsActive;
            location.Status = newStatus;

            _context.Locations.Update(location);
            await _context.SaveChangesAsync();

            return ApiResponse<LocationResponse>.SuccessResponse(
                MapToResponse(location), "Location updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location.");
            return ApiResponse<LocationResponse>.FailureResponse("An error occurred while updating the location.");
        }
    }

    public async Task<ApiResponse<LocationResponse>> CreateLocationAsync(CreateLocationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LocationName))
                return ApiResponse<LocationResponse>.FailureResponse("LocationName is required.");
            if (string.IsNullOrWhiteSpace(request.LocationType))
                return ApiResponse<LocationResponse>.FailureResponse("LocationType is required.");

            var typeError = TryParseLocationType(request.LocationType, out var requestedType);
            if (typeError is not null)
                return ApiResponse<LocationResponse>.FailureResponse(typeError);

            var location = new Location
            {
                LocationName = request.LocationName,
                LocationType = requestedType,
                Address = request.Address,
                IsActive = request.IsActive,
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status,
                // Only the seeder creates system locations; anything created through the API is ordinary.
                IsSystemLocation = false
            };

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            _audit.Record(nameof(Location), location.LocationId.ToString(), "Created",
                nameof(location.LocationName), null, location.LocationName);
            await _context.SaveChangesAsync();

            return ApiResponse<LocationResponse>.SuccessResponse(
                MapToResponse(location), "Location created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location.");
            return ApiResponse<LocationResponse>.FailureResponse("An error occurred while creating the location.");
        }
    }
}
