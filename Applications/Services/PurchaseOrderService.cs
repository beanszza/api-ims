using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<PurchaseOrderService> _logger;

    public PurchaseOrderService(ScmDbContext context, ILogger<PurchaseOrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Creating purchase order for supplier ID {SupplierId}", request.SupplierId);

            // Validate Supplier existence
            var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
            if (supplier == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Supplier with ID {request.SupplierId} not found.");
            }

            // Validate Expected Arrival Date (must be in future)
            if (request.ExpectedArrivalDate <= DateTime.UtcNow)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse("Expected arrival date must be in the future.");
            }

            // Validate Item quantities and existence
            if (request.Items == null || !request.Items.Any())
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse("Purchase order must contain at least one item.");
            }

            var poItems = new List<PurchaseOrderItem>();
            foreach (var itemReq in request.Items)
            {
                if (itemReq.PoItemQuantity <= 0)
                {
                    return ApiResponse<PurchaseOrderResponse>.FailureResponse("Quantity must be greater than zero.");
                }

                var itemExists = await _context.Items.AnyAsync(i => i.ItemId == itemReq.ItemId);
                if (!itemExists)
                {
                    return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Item with ID {itemReq.ItemId} not found.");
                }

                poItems.Add(new PurchaseOrderItem
                {
                    ItemId = itemReq.ItemId,
                    SupplierId = request.SupplierId,
                    PoItemQuantity = itemReq.PoItemQuantity,
                    ReceivedQuantity = 0
                });
            }

            var order = new PurchaseOrder
            {
                SupplierId = request.SupplierId,
                OrderDate = DateTime.UtcNow,
                ExpectedArrivalDate = request.ExpectedArrivalDate,
                Status = "Pending",
                PaymentType = request.PaymentType,
                ProofImageUrl = string.Empty,
                TotalAmount = request.TotalAmount,
                PurchaseOrderItems = poItems
            };

            _context.PurchaseOrders.Add(order);
            await _context.SaveChangesAsync();

            // Reload relationships to return details
            var reloadedOrder = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .FirstOrDefaultAsync(o => o.PoId == order.PoId);

            var response = MapToResponse(reloadedOrder!);
            _logger.LogInformation("Purchase order created successfully with ID {PoId}", order.PoId);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Purchase order created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating purchase order: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<PurchaseOrderResponse>>> GetPurchaseOrdersAsync(string? status = null)
    {
        try
        {
            _logger.LogInformation("Retrieving purchase orders");

            var query = _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.Status.ToLower() == status.ToLower());
            }

            var orders = await query.ToListAsync();
            var responses = orders.Select(MapToResponse);

            return ApiResponse<IEnumerable<PurchaseOrderResponse>>.SuccessResponse(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error retrieving purchase orders: {Message}", ex.Message);
            return ApiResponse<IEnumerable<PurchaseOrderResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> UpdateOrderStatusAsync(int id, string status)
    {
        try
        {
            _logger.LogInformation("Updating purchase order ID {PoId} status to {Status}", id, status);

            var allowedStatuses = new[] { "Pending", "Arrived", "Completed", "Cancelled" };
            var matchedStatus = allowedStatuses.FirstOrDefault(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));

            if (matchedStatus == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Invalid status: {status}. Allowed statuses are: Pending, Arrived, Completed, Cancelled.");
            }

            var order = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");
            }

            var oldStatus = order.Status;
            order.Status = matchedStatus;

            // Trigger stock additions & transaction logging on "Arrived" or "Completed"
            var isArrivingTransition = (matchedStatus == "Arrived" || matchedStatus == "Completed") && oldStatus != "Arrived" && oldStatus != "Completed";

            if (isArrivingTransition)
            {
                // Retrieve default Location and Driver as fallback
                var location = await _context.Locations.FirstOrDefaultAsync();
                if (location == null)
                {
                    location = new Location { LocationName = "Main Warehouse", LocationType = "Storage" };
                    _context.Locations.Add(location);
                    await _context.SaveChangesAsync();
                }

                var driver = await _context.Drivers.FirstOrDefaultAsync();
                if (driver == null)
                {
                    driver = new Driver { DriverName = "Default Driver", Number = "DRV-001" };
                    _context.Drivers.Add(driver);
                    await _context.SaveChangesAsync();
                }

                foreach (var poItem in order.PurchaseOrderItems)
                {
                    poItem.ReceivedQuantity = poItem.PoItemQuantity;

                    // Update or Add Inventory
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(inv => inv.ItemId == poItem.ItemId && inv.LocationId == location.LocationId);

                    if (inventory != null)
                    {
                        inventory.CurrentStock += poItem.PoItemQuantity;
                        _context.Inventories.Update(inventory);
                    }
                    else
                    {
                        inventory = new Inventory
                        {
                            ItemId = poItem.ItemId,
                            LocationId = location.LocationId,
                            DriverId = driver.DriverId,
                            CurrentStock = poItem.PoItemQuantity
                        };
                        _context.Inventories.Add(inventory);
                    }

                    // Log Inventory Movement Transaction
                    var movementLog = new InventoryMovementLog
                    {
                        ItemId = poItem.ItemId,
                        LocationId = location.LocationId,
                        ChangeQuantity = poItem.PoItemQuantity,
                        ActionType = "Order Arrival",
                        ReferenceId = order.PoId.ToString(),
                        UserId = 1,
                        Timestamp = DateTime.UtcNow
                    };
                    _context.InventoryMovementLogs.Add(movementLog);
                }
            }

            _context.PurchaseOrders.Update(order);
            await _context.SaveChangesAsync();

            var response = MapToResponse(order);
            _logger.LogInformation("Purchase order ID {PoId} updated successfully", id);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Status updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating purchase order status: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<PurchaseOrderResponse>> UploadReceiptAsync(int id, IFormFile file)
    {
        try
        {
            _logger.LogInformation("Uploading receipt for purchase order ID {PoId}", id);

            var order = await _context.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.PurchaseOrderItems)
                .ThenInclude(poi => poi.Item)
                .FirstOrDefaultAsync(o => o.PoId == id);

            if (order == null)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse($"Purchase order with ID {id} not found.");
            }

            if (file == null || file.Length == 0)
            {
                return ApiResponse<PurchaseOrderResponse>.FailureResponse("No file was uploaded.");
            }

            // Create target folder in wwwroot
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "receipts");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var extension = Path.GetExtension(file.FileName);
            var uniqueFileName = $"receipt_{id}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            order.ProofImageUrl = $"/receipts/{uniqueFileName}";
            _context.PurchaseOrders.Update(order);
            await _context.SaveChangesAsync();

            var response = MapToResponse(order);
            _logger.LogInformation("Receipt uploaded successfully for purchase order ID {PoId}", id);
            return ApiResponse<PurchaseOrderResponse>.SuccessResponse(response, "Receipt attachment uploaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error uploading receipt: {Message}", ex.Message);
            return ApiResponse<PurchaseOrderResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<TransactionHistoryResponse>>> GetTransactionHistoryAsync(string? filterType = null, DateTime? specificDate = null)
    {
        try
        {
            _logger.LogInformation("Fetching transaction history");

            var query = _context.InventoryMovementLogs
                .Include(l => l.Item)
                .Include(l => l.Location)
                .AsQueryable();

            query = ApplyFilter(query, filterType, specificDate);

            var logs = await query.ToListAsync();
            var responses = logs.Select(MapToTransactionResponse);

            return ApiResponse<IEnumerable<TransactionHistoryResponse>>.SuccessResponse(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error retrieving transaction history: {Message}", ex.Message);
            return ApiResponse<IEnumerable<TransactionHistoryResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<string> ExportTransactionHistoryCsvAsync(string? filterType = null, DateTime? specificDate = null)
    {
        try
        {
            _logger.LogInformation("Exporting transaction history to CSV");

            var query = _context.InventoryMovementLogs
                .Include(l => l.Item)
                .Include(l => l.Location)
                .AsQueryable();

            query = ApplyFilter(query, filterType, specificDate);

            var logs = await query.ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("MovementId,ItemId,ItemName,LocationName,ChangeQuantity,ActionType,ReferenceId,UserId,Timestamp");

            foreach (var log in logs)
            {
                var itemName = EscapeCsv(log.Item?.ItemName ?? string.Empty);
                var locationName = EscapeCsv(log.Location?.LocationName ?? string.Empty);
                var actionType = EscapeCsv(log.ActionType);
                var referenceId = EscapeCsv(log.ReferenceId);

                builder.AppendLine($"{log.MovementId},{log.ItemId},{itemName},{locationName},{log.ChangeQuantity},{actionType},{referenceId},{log.UserId},{log.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            return builder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error exporting transaction history to CSV: {Message}", ex.Message);
            throw;
        }
    }

    private static IQueryable<InventoryMovementLog> ApplyFilter(IQueryable<InventoryMovementLog> query, string? filterType, DateTime? specificDate)
    {
        if (string.IsNullOrWhiteSpace(filterType))
        {
            return query;
        }

        switch (filterType.ToLower())
        {
            case "day":
                var dayLimit = DateTime.UtcNow.Date;
                query = query.Where(l => l.Timestamp >= dayLimit);
                break;
            case "week":
                var weekLimit = DateTime.UtcNow.Date.AddDays(-7);
                query = query.Where(l => l.Timestamp >= weekLimit);
                break;
            case "month":
                var monthLimit = DateTime.UtcNow.Date.AddDays(-30);
                query = query.Where(l => l.Timestamp >= monthLimit);
                break;
            case "year":
                var yearLimit = DateTime.UtcNow.Date.AddDays(-365);
                query = query.Where(l => l.Timestamp >= yearLimit);
                break;
            case "specificdate":
                if (specificDate.HasValue)
                {
                    var specDate = specificDate.Value.Date;
                    query = query.Where(l => l.Timestamp.Date == specDate);
                }
                break;
        }

        return query;
    }

    private static PurchaseOrderResponse MapToResponse(PurchaseOrder order)
    {
        return new PurchaseOrderResponse
        {
            PoId = order.PoId,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.CompanyName ?? string.Empty,
            OrderDate = order.OrderDate,
            ExpectedArrivalDate = order.ExpectedArrivalDate,
            Status = order.Status,
            PaymentType = order.PaymentType,
            ProofImageUrl = order.ProofImageUrl,
            TotalAmount = order.TotalAmount,
            Items = order.PurchaseOrderItems.Select(poi => new PurchaseOrderItemResponse
            {
                PoItemId = poi.PoItemId,
                ItemId = poi.ItemId,
                ItemName = poi.Item?.ItemName ?? string.Empty,
                PoItemQuantity = poi.PoItemQuantity,
                ReceivedQuantity = poi.ReceivedQuantity
            }).ToList()
        };
    }

    private static TransactionHistoryResponse MapToTransactionResponse(InventoryMovementLog log)
    {
        return new TransactionHistoryResponse
        {
            MovementId = log.MovementId,
            ItemId = log.ItemId,
            ItemName = log.Item?.ItemName ?? string.Empty,
            LocationName = log.Location?.LocationName ?? string.Empty,
            ChangeQuantity = log.ChangeQuantity,
            ActionType = log.ActionType,
            ReferenceId = log.ReferenceId,
            UserId = log.UserId,
            Timestamp = log.Timestamp
        };
    }

    private static string EscapeCsv(string field)
    {
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }
}
