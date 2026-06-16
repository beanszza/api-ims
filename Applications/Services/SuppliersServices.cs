using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Applications.Interfaces;
using Microsoft.Extensions.Logging; 
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace Applications.Services;

public class SupplierService : ISupplierService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(ScmDbContext context, ILogger<SupplierService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedData<SupplierResponse>>> GetAllSuppliersAsync(string? supplierName = null, bool? isActive = null, int page = 1, int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("Fetching all suppliers");

            var query = _context.Suppliers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                query = query.Where(s => s.CompanyName.ToLower().Contains(supplierName.ToLower()));
            }

            if (isActive.HasValue)
            {
                query = query.Where(s => s.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();
            var suppliers = await query
                .Select(s => new SupplierResponse
                {
                    SupplierId = s.SupplierId,
                    CompanyName = s.CompanyName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    Phone = s.Phone,
                    IsActive = s.IsActive
                })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedData = new PagedData<SupplierResponse>
            {
                Items = suppliers,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            _logger.LogInformation($"Successfully fetched {suppliers.Count} suppliers");
            return ApiResponse<PagedData<SupplierResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching suppliers: {ex.Message}");
            return ApiResponse<PagedData<SupplierResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SupplierResponse>> GetSupplierByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation($"Fetching supplier with ID: {id}");

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
            {
                _logger.LogWarning($"Supplier with ID {id} not found");
                return ApiResponse<SupplierResponse>.FailureResponse("Supplier not found");
            }

            var response = new SupplierResponse
            {
                SupplierId = supplier.SupplierId,
                CompanyName = supplier.CompanyName,
                ContactPerson = supplier.ContactPerson,
                Email = supplier.Email,
                Phone = supplier.Phone,
                IsActive = supplier.IsActive
            };

            return ApiResponse<SupplierResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching supplier: {ex.Message}");
            return ApiResponse<SupplierResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SupplierResponse>> CreateSupplierAsync(CreateSupplierRequest request)
    {
        try
        {
            _logger.LogInformation($"Creating new supplier: {request.CompanyName}");

            //Validate Email Format
            if (!Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Invalid email format.");
            }

            //Validate Phone Number (Basic validation allowing numbers, +, -, and spaces)
            if (!Regex.IsMatch(request.Phone, @"^[\+\d\s\-]{7,15}$"))
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Invalid phone number format.");
            }

            //Prevent Duplicate Supplier Names
            var supplierExists = await _context.Suppliers
                .AnyAsync(s => s.CompanyName.ToLower() == request.CompanyName.ToLower());

            if (supplierExists)
            {
                return ApiResponse<SupplierResponse>.FailureResponse("A supplier with this name already exists.");
            }

            // <-- FIX 2: Instantiate the supplier variable before using it
            var supplier = new Supplier
            {
                CompanyName = request.CompanyName,
                ContactPerson = request.ContactPerson,
                Email = request.Email,
                Phone = request.Phone,
                IsActive = request.IsActive
            };

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            var response = new SupplierResponse
            {
                SupplierId = supplier.SupplierId,
                CompanyName = supplier.CompanyName,
                ContactPerson = supplier.ContactPerson,
                Email = supplier.Email,
                Phone = supplier.Phone,
                IsActive = supplier.IsActive
            };

            _logger.LogInformation($"Supplier created successfully with ID: {supplier.SupplierId}");
            return ApiResponse<SupplierResponse>.SuccessResponse(response, "Supplier created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating supplier: {ex.Message}");
            return ApiResponse<SupplierResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SupplierResponse>> UpdateSupplierAsync(int id, UpdateSupplierRequest request)
    {
        try
        {
            _logger.LogInformation($"Updating supplier with ID: {id}");

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                _logger.LogWarning($"Supplier with ID {id} not found");
                return ApiResponse<SupplierResponse>.FailureResponse("Supplier not found");
            }

            if (request.CompanyName != null)
                supplier.CompanyName = request.CompanyName;
            if (request.ContactPerson != null)
                supplier.ContactPerson = request.ContactPerson;
            
            if (request.Email != null)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return ApiResponse<SupplierResponse>.FailureResponse("Invalid email format.");
                }
                supplier.Email = request.Email;
            }

            if (request.Phone != null)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Phone, @"^[\+\d\s\-]{7,15}$"))
                {
                    return ApiResponse<SupplierResponse>.FailureResponse("Invalid phone number format.");
                }
                supplier.Phone = request.Phone;
            }

            if (request.IsActive.HasValue)
                supplier.IsActive = request.IsActive.Value;

            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();

            var response = new SupplierResponse
            {
                SupplierId = supplier.SupplierId,
                CompanyName = supplier.CompanyName,
                ContactPerson = supplier.ContactPerson,
                Email = supplier.Email,
                Phone = supplier.Phone,
                IsActive = supplier.IsActive
            };

            _logger.LogInformation($"Supplier with ID {id} updated successfully. Time of change: {DateTime.UtcNow}");
            return ApiResponse<SupplierResponse>.SuccessResponse(response, "Supplier updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating supplier: {ex.Message}");
            return ApiResponse<SupplierResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EmptyPayload>> DeleteSupplierAsync(int id)
    {
        try
        {
            _logger.LogInformation($"Deleting supplier with ID: {id}");

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                _logger.LogWarning($"Supplier with ID {id} not found");
                return ApiResponse<EmptyPayload>.FailureResponse("Supplier not found");
            }

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Supplier with ID {id} deleted successfully");
            return ApiResponse<EmptyPayload>.SuccessResponse(new EmptyPayload(), "Supplier deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting supplier: {ex.Message}");
            return ApiResponse<EmptyPayload>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }
}