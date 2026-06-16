using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class LocationService : ILocationService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<LocationService> _logger;

    public LocationService(ScmDbContext context, ILogger<LocationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<IEnumerable<LocationResponse>>> GetAllLocationsAsync()
    {
        try
        {
            var locations = await _context.Locations
                .Select(l => new LocationResponse
                {
                    LocationId = l.LocationId,
                    LocationName = l.LocationName,
                    LocationType = l.LocationType,
                    Status = l.Status
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<LocationResponse>>.SuccessResponse(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations.");
            return ApiResponse<IEnumerable<LocationResponse>>.FailureResponse("An error occurred while fetching locations.");
        }
    }

    public async Task<ApiResponse<LocationResponse>> UpdateLocationAsync(int id, UpdateLocationRequest request, int userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LocationName))
                return ApiResponse<LocationResponse>.FailureResponse("LocationName is required.");
            if (string.IsNullOrWhiteSpace(request.LocationType))
                return ApiResponse<LocationResponse>.FailureResponse("LocationType is required.");

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.LocationId == id);
            
            if (location == null)
                return ApiResponse<LocationResponse>.FailureResponse("Location not found.");

            // Log changes to AuditLog
            var timestamp = DateTime.UtcNow;

            if (location.LocationName != request.LocationName)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Location",
                    EntityId = location.LocationId.ToString(),
                    FieldName = "LocationName",
                    OldValue = location.LocationName,
                    NewValue = request.LocationName,
                    Action = "Updated",
                    Timestamp = timestamp,
                    UserId = userId
                });
            }

            if (location.LocationType != request.LocationType)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Location",
                    EntityId = location.LocationId.ToString(),
                    FieldName = "LocationType",
                    OldValue = location.LocationType,
                    NewValue = request.LocationType,
                    Action = "Updated",
                    Timestamp = timestamp,
                    UserId = userId
                });
            }

            var newStatus = string.IsNullOrWhiteSpace(request.Status) ? location.Status : request.Status;
            if (location.Status != newStatus)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Location",
                    EntityId = location.LocationId.ToString(),
                    FieldName = "Status",
                    OldValue = location.Status ?? "Active",
                    NewValue = newStatus,
                    Action = "StatusUpdated",
                    Timestamp = timestamp,
                    UserId = userId
                });
            }

            location.LocationName = request.LocationName;
            location.LocationType = request.LocationType;
            location.Status = newStatus;

            _context.Locations.Update(location);
            await _context.SaveChangesAsync();

            var response = new LocationResponse
            {
                LocationId = location.LocationId,
                LocationName = location.LocationName,
                LocationType = location.LocationType,
                Status = location.Status
            };

            return ApiResponse<LocationResponse>.SuccessResponse(response, "Location updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location.");
            return ApiResponse<LocationResponse>.FailureResponse("An error occurred while updating the location.");
        }
    }
}
