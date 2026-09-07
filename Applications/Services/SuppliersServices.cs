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
    private readonly IDocumentNumberService _documentNumberService;

    public SupplierService(ScmDbContext context, ILogger<SupplierService> logger, IDocumentNumberService documentNumberService)
    {
        _context = context;
        _logger = logger;
        _documentNumberService = documentNumberService;
    }

    public async Task<ApiResponse<PagedData<SupplierResponse>>> GetAllSuppliersAsync(string? supplierName = null, bool? isActive = null, int page = 1, int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("Fetching all suppliers");

            var query = _context.Suppliers
                .Include(s => s.SupplierItems)
                .ThenInclude(si => si.Item)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var lowerSearch = supplierName.ToLower();
                query = query.Where(s =>
                    s.CompanyName.ToLower().Contains(lowerSearch) ||
                    (!string.IsNullOrEmpty(s.SupplierCode) && s.SupplierCode.ToLower().Contains(lowerSearch)) ||
                    (!string.IsNullOrEmpty(s.ContactPerson) && s.ContactPerson.ToLower().Contains(lowerSearch)) ||
                    (!string.IsNullOrEmpty(s.Email) && s.Email.ToLower().Contains(lowerSearch)) ||
                    s.SupplierId.ToString().Contains(lowerSearch)
                );
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
                    SupplierCode = s.SupplierCode,
                    CompanyName = s.CompanyName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    Phone = s.Phone,
                    Address = s.Address,
                    Website = s.Website,
                    IsActive = s.IsActive,
                    SuppliedItems = s.SupplierItems.Select(si => new SupplierSupplyItemResponse
                    {
                        ItemId = si.ItemId,
                        ItemName = si.Item.ItemName
                    }).ToList()
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
                .Include(s => s.SupplierItems)
                .ThenInclude(si => si.Item)
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier == null)
            {
                _logger.LogWarning($"Supplier with ID {id} not found");
                return ApiResponse<SupplierResponse>.FailureResponse("Supplier not found");
            }

            var response = new SupplierResponse
            {
                SupplierId = supplier.SupplierId,
                SupplierCode = supplier.SupplierCode,
                CompanyName = supplier.CompanyName,
                ContactPerson = supplier.ContactPerson,
                Email = supplier.Email,
                Phone = supplier.Phone,
                IsActive = supplier.IsActive,
                SuppliedItems = supplier.SupplierItems.Select(si => new SupplierSupplyItemResponse
                {
                    ItemId = si.ItemId,
                    ItemName = si.Item?.ItemName ?? string.Empty
                }).ToList()
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
            if (string.IsNullOrWhiteSpace(request.CompanyName) || request.CompanyName.Trim().Length > 50)
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Supplier Name is required and cannot exceed 50 characters.");
            }

            if (string.IsNullOrWhiteSpace(request.ContactPerson) || request.ContactPerson.Trim().Length > 50)
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Contact Person is required and cannot exceed 50 characters.");
            }

            if (Regex.IsMatch(request.ContactPerson, @"\d"))
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Contact Person cannot contain numbers.");
            }

            //Validate Email Format (max 50 chars)
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 50 || !Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Invalid email format (max 50 characters).");
            }

            //Validate Phone Number (Philippine format, max 50 chars)
            if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Length > 50 || !Regex.IsMatch(request.Phone, @"^[\+\d\s\-]{7,20}$"))
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Invalid phone number format.");
            }

            //Validate Address (max 100 chars)
            if (string.IsNullOrWhiteSpace(request.Address) || request.Address.Length > 100)
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Address is required and cannot exceed 100 characters.");
            }

            if (!string.IsNullOrWhiteSpace(request.Website) && request.Website.Length > 50)
            {
                return ApiResponse<SupplierResponse>.FailureResponse("Website cannot exceed 50 characters.");
            }

            //Prevent Duplicate Supplier Names
            var supplierExists = await _context.Suppliers
                .AnyAsync(s => s.CompanyName.ToLower() == request.CompanyName.ToLower());

            if (supplierExists)
            {
                return ApiResponse<SupplierResponse>.FailureResponse("A supplier with this name already exists.");
            }

            var supplierCode = await _documentNumberService.NextAsync(Domains.Enums.DocumentType.Supplier);
            
            // <-- FIX 2: Instantiate the supplier variable before using it
            var supplier = new Supplier
            {
                SupplierCode = supplierCode,
                CompanyName = request.CompanyName,
                ContactPerson = request.ContactPerson,
                Email = request.Email,
                Phone = request.Phone,
                Address = request.Address,
                Website = request.Website,
                IsActive = request.IsActive
            };

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            var response = new SupplierResponse
            {
                SupplierId = supplier.SupplierId,
                SupplierCode = supplier.SupplierCode,
                CompanyName = supplier.CompanyName,
                ContactPerson = supplier.ContactPerson,
                Email = supplier.Email,
                Phone = supplier.Phone,
                Address = supplier.Address,
                Website = supplier.Website,
                IsActive = supplier.IsActive
            };

            if (request.SuppliedItemIds != null && request.SuppliedItemIds.Any())
            {
                var items = await _context.Items.Where(i => request.SuppliedItemIds.Contains(i.ItemId)).ToListAsync();
                foreach (var item in items)
                {
                    _context.SupplierItems.Add(new Domains.Entities.SupplierItem
                    {
                        SupplierId = supplier.SupplierId,
                        ItemId = item.ItemId,
                        PurchaseUomId = item.UomId,
                        UnitPrice = 0,
                        PackSize = 1
                    });
                }
                await _context.SaveChangesAsync();

                response.SuppliedItems = items.Select(i => new SupplierSupplyItemResponse
                {
                    ItemId = i.ItemId,
                    ItemName = i.ItemName
                }).ToList();
            }

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
            {
                if (string.IsNullOrWhiteSpace(request.CompanyName) || request.CompanyName.Trim().Length > 50)
                    return ApiResponse<SupplierResponse>.FailureResponse("Supplier Name cannot exceed 50 characters.");
                supplier.CompanyName = request.CompanyName;
            }

            if (request.ContactPerson != null)
            {
                if (string.IsNullOrWhiteSpace(request.ContactPerson) || request.ContactPerson.Trim().Length > 50)
                    return ApiResponse<SupplierResponse>.FailureResponse("Contact Person cannot exceed 50 characters.");
                if (Regex.IsMatch(request.ContactPerson, @"\d"))
                    return ApiResponse<SupplierResponse>.FailureResponse("Contact Person cannot contain numbers.");
                supplier.ContactPerson = request.ContactPerson;
            }
            
            if (request.Email != null)
            {
                if (request.Email.Length > 50 || !Regex.IsMatch(request.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                {
                    return ApiResponse<SupplierResponse>.FailureResponse("Invalid email format (max 50 characters).");
                }
                supplier.Email = request.Email;
            }

            if (request.Phone != null)
            {
                if (request.Phone.Length > 50 || !Regex.IsMatch(request.Phone, @"^[\+\d\s\-]{7,20}$"))
                {
                    return ApiResponse<SupplierResponse>.FailureResponse("Invalid phone number format.");
                }
                supplier.Phone = request.Phone;
            }

            if (request.Address != null)
            {
                if (string.IsNullOrWhiteSpace(request.Address) || request.Address.Length > 100)
                    return ApiResponse<SupplierResponse>.FailureResponse("Address cannot exceed 100 characters.");
                supplier.Address = request.Address;
            }

            if (request.Website != null)
            {
                if (request.Website.Length > 50)
                    return ApiResponse<SupplierResponse>.FailureResponse("Website cannot exceed 50 characters.");
                supplier.Website = request.Website;
            }

            if (request.IsActive.HasValue)
                supplier.IsActive = request.IsActive.Value;

            if (request.SuppliedItemIds != null)
            {
                _context.SupplierItems.RemoveRange(supplier.SupplierItems);
                
                var items = await _context.Items.Where(i => request.SuppliedItemIds.Contains(i.ItemId)).ToListAsync();
                foreach (var item in items)
                {
                    _context.SupplierItems.Add(new Domains.Entities.SupplierItem
                    {
                        SupplierId = supplier.SupplierId,
                        ItemId = item.ItemId,
                        PurchaseUomId = item.UomId,
                        UnitPrice = 0,
                        PackSize = 1
                    });
                }
            }

            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();

            // reload supplier items for response
            supplier = await _context.Suppliers
                .Include(s => s.SupplierItems)
                .ThenInclude(si => si.Item)
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            var response = new SupplierResponse
            {
                SupplierId = supplier.SupplierId,
                SupplierCode = supplier.SupplierCode,
                CompanyName = supplier.CompanyName,
                ContactPerson = supplier.ContactPerson,
                Email = supplier.Email,
                Phone = supplier.Phone,
                Address = supplier.Address,
                Website = supplier.Website,
                IsActive = supplier.IsActive,
                SuppliedItems = supplier.SupplierItems.Select(si => new SupplierSupplyItemResponse
                {
                    ItemId = si.ItemId,
                    ItemName = si.Item?.ItemName ?? string.Empty
                }).ToList()
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