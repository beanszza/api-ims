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

    public async Task<ApiResponse<PagedData<LocationResponse>>> GetAllLocationsAsync(int page = 1, int pageSize = 10)
    {
        try
        {
            var totalCount = await _context.Locations.CountAsync();
            var locations = await _context.Locations
                .Select(l => new LocationResponse
                {
                    LocationId = l.LocationId,
                    LocationName = l.LocationName,
                    LocationType = l.LocationType,
                    Address = l.Address,
                    IsActive = l.IsActive,
                    Status = l.Status
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.LocationId == id);
            
            if (location == null)
                return ApiResponse<LocationResponse>.FailureResponse("Location not found.");

            // Field-level audit entries, attributed to whoever is making the request.
            var locationId = location.LocationId.ToString();

            if (location.LocationName != request.LocationName)
            {
                _audit.Record(nameof(Location), locationId, "Updated",
                    nameof(location.LocationName), location.LocationName, request.LocationName);
            }

            if (location.LocationType != request.LocationType)
            {
                _audit.Record(nameof(Location), locationId, "Updated",
                    nameof(location.LocationType), location.LocationType, request.LocationType);
            }

            var newStatus = string.IsNullOrWhiteSpace(request.Status) ? location.Status : request.Status;
            if (location.Status != newStatus)
            {
                _audit.Record(nameof(Location), locationId, "StatusUpdated",
                    nameof(location.Status), location.Status ?? "Active", newStatus);
            }

            location.LocationName = request.LocationName;
            location.LocationType = request.LocationType;
            location.Address = request.Address;
            location.IsActive = request.IsActive;
            location.Status = newStatus;

            _context.Locations.Update(location);
            await _context.SaveChangesAsync();

            var response = new LocationResponse
            {
                LocationId = location.LocationId,
                LocationName = location.LocationName,
                LocationType = location.LocationType,
                Address = location.Address,
                IsActive = location.IsActive,
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

    public async Task<ApiResponse<LocationResponse>> CreateLocationAsync(CreateLocationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LocationName))
                return ApiResponse<LocationResponse>.FailureResponse("LocationName is required.");
            if (string.IsNullOrWhiteSpace(request.LocationType))
                return ApiResponse<LocationResponse>.FailureResponse("LocationType is required.");

            var location = new Location
            {
                LocationName = request.LocationName,
                LocationType = request.LocationType,
                Address = request.Address,
                IsActive = request.IsActive,
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status
            };

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            _audit.Record(nameof(Location), location.LocationId.ToString(), "Created",
                nameof(location.LocationName), null, location.LocationName);
            await _context.SaveChangesAsync();

            var response = new LocationResponse
            {
                LocationId = location.LocationId,
                LocationName = location.LocationName,
                LocationType = location.LocationType,
                Address = location.Address,
                IsActive = location.IsActive,
                Status = location.Status
            };

            return ApiResponse<LocationResponse>.SuccessResponse(response, "Location created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location.");
            return ApiResponse<LocationResponse>.FailureResponse("An error occurred while creating the location.");
        }
    }
}
