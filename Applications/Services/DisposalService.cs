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

public class DisposalService : IDisposalService
{
    private readonly ScmDbContext _context;
    private readonly IStockPostingService _stockPosting;
    private readonly IPostingTransaction _posting;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<DisposalService> _logger;

    public DisposalService(
        ScmDbContext context,
        IStockPostingService stockPosting,
        IPostingTransaction posting,
        IDocumentNumberService documentNumbers,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<DisposalService> logger)
    {
        _context = context;
        _stockPosting = stockPosting;
        _posting = posting;
        _documentNumbers = documentNumbers;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<ExpirySweepResponse>> PerformExpirySweepAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.UtcNow;

            // Find all available lots that have passed expiry date
            var expiredLots = await _context.InventoryLots
                .Include(l => l.Item)
                .Include(l => l.Location)
                .Where(l => l.Status == LotStatus.Available &&
                            l.QuantityRemaining > 0 &&
                            l.ExpiryDate != null &&
                            l.ExpiryDate <= today)
                .ToListAsync();

            if (!expiredLots.Any())
            {
                return ApiResponse<ExpirySweepResponse>.SuccessResponse(new ExpirySweepResponse
                {
                    SweptAt = now,
                    ExpiredLotsCount = 0,
                    TotalQuantityQuarantined = 0m,
                    TotalEstimatedLoss = 0m,
                    ExpiredLots = new List<ExpiredLotDetail>()
                }, "Expiry sweep completed. No expired lots detected.");
            }

            var expiredDetails = new List<ExpiredLotDetail>();
            decimal totalQty = 0m;
            decimal totalLoss = 0m;

            await _posting.ExecuteAsync(async () =>
            {
                foreach (var lot in expiredLots)
                {
                    var qty = lot.QuantityRemaining;
                    var cost = lot.UnitCost * qty;

                    totalQty += qty;
                    totalLoss += cost;

                    // Transition lot status to Expired
                    lot.Status = LotStatus.Expired;
                    _context.InventoryLots.Update(lot);

                    // Debit from available inventory cache
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ItemId == lot.ItemId && i.LocationId == lot.LocationId);

                    if (inventory != null)
                    {
                        inventory.CurrentStock = Math.Max(0m, inventory.CurrentStock - qty);
                        _context.Inventories.Update(inventory);
                    }

                    _context.StockLedgers.Add(new StockLedger
                    {
                        LotId = lot.LotId,
                        ItemId = lot.ItemId,
                        LocationId = lot.LocationId,
                        MovementType = MovementType.Disposal,
                        Quantity = -qty,
                        UomId = lot.UomId,
                        UnitCost = lot.UnitCost,
                        ReferenceType = "ExpirySweep",
                        ReferenceId = today.ToString("yyyyMMdd"),
                        UserId = _currentUser.Current.UserId,
                        UserName = _currentUser.Current.AuditName,
                        PostedAt = now,
                        Notes = $"Automated sweep: Lot expired on {lot.ExpiryDate:yyyy-MM-dd}"
                    });

                    expiredDetails.Add(new ExpiredLotDetail
                    {
                        LotId = lot.LotId,
                        LotCode = lot.LotCode,
                        ItemId = lot.ItemId,
                        ItemName = lot.Item?.ItemName ?? $"Item {lot.ItemId}",
                        LocationId = lot.LocationId,
                        LocationName = lot.Location?.LocationName ?? $"Location {lot.LocationId}",
                        ExpiryDate = lot.ExpiryDate,
                        QuantityExpired = qty,
                        UnitCost = lot.UnitCost
                    });
                }

                _audit.Record(
                    nameof(InventoryLot),
                    today.ToString("yyyy-MM-dd"),
                    "ExpirySweepExecuted",
                    fieldName: "Status",
                    oldValue: "Available",
                    newValue: "Expired");

                return expiredDetails;
            });

            var response = new ExpirySweepResponse
            {
                SweptAt = now,
                ExpiredLotsCount = expiredLots.Count,
                TotalQuantityQuarantined = totalQty,
                TotalEstimatedLoss = totalLoss,
                ExpiredLots = expiredDetails
            };

            return ApiResponse<ExpirySweepResponse>.SuccessResponse(
                response,
                $"Expiry sweep completed. {expiredLots.Count} lots quarantined due to expiry (Total: {totalQty} units, ₱{totalLoss:N2} loss).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing expiry sweep");
            return ApiResponse<ExpirySweepResponse>.FailureResponse($"Expiry sweep failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<DisposalRecordResponse>>> GetDisposalRecordsAsync()
    {
        try
        {
            var list = await _context.DisposalRecords
                .Include(d => d.Items).ThenInclude(i => i.Item)
                .Include(d => d.Items).ThenInclude(i => i.Lot)
                .OrderByDescending(d => d.DisposalId)
                .ToListAsync();

            return ApiResponse<List<DisposalRecordResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disposal records");
            return ApiResponse<List<DisposalRecordResponse>>.FailureResponse("An error occurred while fetching disposal records.");
        }
    }

    public async Task<ApiResponse<DisposalRecordResponse>> GetDisposalRecordByIdAsync(int disposalId)
    {
        try
        {
            var record = await _context.DisposalRecords
                .Include(d => d.Items).ThenInclude(i => i.Item)
                .Include(d => d.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(d => d.DisposalId == disposalId);

            if (record == null)
                return ApiResponse<DisposalRecordResponse>.FailureResponse($"Disposal record {disposalId} not found.");

            return ApiResponse<DisposalRecordResponse>.SuccessResponse(MapToResponse(record));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disposal record {DisposalId}", disposalId);
            return ApiResponse<DisposalRecordResponse>.FailureResponse("An error occurred while fetching disposal record.");
        }
    }

    public async Task<ApiResponse<DisposalRecordResponse>> CreateDisposalRecordAsync(CreateDisposalRequest request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<DisposalRecordResponse>.FailureResponse("Disposal record must contain at least one item.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            var disposalNumber = await _documentNumbers.NextAsync(DocumentType.Disposal, now);

            var record = new DisposalRecord
            {
                DisposalNumber = disposalNumber,
                DisposalDate = now,
                Reason = request.Reason.Trim(),
                AuthorizedBy = actor.AuditName,
                WitnessName = request.WitnessName,
                Notes = request.Notes
            };

            var recordItems = new List<DisposalRecordItem>();
            var draws = new List<LotDraw>();
            decimal totalCost = 0m;

            await _posting.ExecuteAsync(async () =>
            {
                foreach (var itemReq in request.Items)
                {
                    var lot = await _context.InventoryLots
                        .Include(l => l.Item)
                        .FirstOrDefaultAsync(l => l.LotId == itemReq.LotId);

                    if (lot == null)
                        throw new InvalidOperationException($"Lot {itemReq.LotId} not found.");

                    if (itemReq.QuantityDisposed > lot.QuantityRemaining)
                        throw new InvalidOperationException($"Cannot dispose {itemReq.QuantityDisposed} from lot {lot.LotCode} (Only {lot.QuantityRemaining} remaining).");

                    draws.Add(new LotDraw(lot.LotId, itemReq.QuantityDisposed));
                    var lineCost = itemReq.QuantityDisposed * lot.UnitCost;
                    totalCost += lineCost;

                    recordItems.Add(new DisposalRecordItem
                    {
                        LotId = lot.LotId,
                        ItemId = lot.ItemId,
                        QuantityDisposed = itemReq.QuantityDisposed,
                        UnitCost = lot.UnitCost,
                        Notes = itemReq.Notes
                    });
                }

                // Execute physical ledger write-offs via stock posting service
                await _stockPosting.DisposeAsync(draws, request.Reason, nameof(DisposalRecord), disposalNumber);

                record.TotalCost = totalCost;
                record.Items = recordItems;
                _context.DisposalRecords.Add(record);

                _audit.Record(
                    nameof(DisposalRecord),
                    record.DisposalNumber,
                    "DisposalRecorded",
                    fieldName: "TotalCost",
                    oldValue: null,
                    newValue: totalCost.ToString("F2"));

                return record;
            });

            var reloaded = await _context.DisposalRecords
                .Include(d => d.Items).ThenInclude(i => i.Item)
                .Include(d => d.Items).ThenInclude(i => i.Lot)
                .FirstAsync(d => d.DisposalId == record.DisposalId);

            return ApiResponse<DisposalRecordResponse>.SuccessResponse(
                MapToResponse(reloaded),
                $"Disposal record {record.DisposalNumber} posted successfully. Total written off: ₱{totalCost:N2}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording disposal");
            return ApiResponse<DisposalRecordResponse>.FailureResponse($"Failed to create disposal record: {ex.Message}");
        }
    }

    private static DisposalRecordResponse MapToResponse(DisposalRecord d) => new()
    {
        DisposalId = d.DisposalId,
        DisposalNumber = d.DisposalNumber,
        DisposalDate = d.DisposalDate,
        Reason = d.Reason,
        TotalCost = d.TotalCost,
        AuthorizedBy = d.AuthorizedBy,
        WitnessName = d.WitnessName,
        Notes = d.Notes,
        Items = d.Items.Select(i => new DisposalRecordItemResponse
        {
            DisposalItemId = i.DisposalItemId,
            LotId = i.LotId,
            LotCode = i.Lot?.LotCode ?? $"LOT-{i.LotId}",
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            QuantityDisposed = i.QuantityDisposed,
            UnitCost = i.UnitCost,
            Notes = i.Notes
        }).ToList()
    };
}
