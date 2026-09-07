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

public class CycleCountService : ICycleCountService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<CycleCountService> _logger;

    public CycleCountService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<CycleCountService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<CycleCountResponse>>> GetCycleCountsAsync(int? locationId = null)
    {
        try
        {
            var query = _context.CycleCounts
                .Include(c => c.Location)
                .Include(c => c.Items).ThenInclude(i => i.Item)
                .Include(c => c.Items).ThenInclude(i => i.Lot)
                .AsQueryable();

            if (locationId.HasValue && locationId.Value > 0)
                query = query.Where(c => c.LocationId == locationId.Value);

            var list = await query.OrderByDescending(c => c.CycleCountId).ToListAsync();
            return ApiResponse<List<CycleCountResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cycle counts");
            return ApiResponse<List<CycleCountResponse>>.FailureResponse("An error occurred while fetching cycle counts.");
        }
    }

    public async Task<ApiResponse<CycleCountResponse>> GetCycleCountByIdAsync(int countId)
    {
        try
        {
            var cc = await _context.CycleCounts
                .Include(c => c.Location)
                .Include(c => c.Items).ThenInclude(i => i.Item)
                .Include(c => c.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(c => c.CycleCountId == countId);

            if (cc == null)
                return ApiResponse<CycleCountResponse>.FailureResponse($"Cycle count {countId} not found.");

            return ApiResponse<CycleCountResponse>.SuccessResponse(MapToResponse(cc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cycle count {CountId}", countId);
            return ApiResponse<CycleCountResponse>.FailureResponse("An error occurred while fetching cycle count.");
        }
    }

    public async Task<ApiResponse<CycleCountResponse>> CreateCycleCountAsync(CreateCycleCountRequest request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<CycleCountResponse>.FailureResponse("Cycle count must contain at least one item.");

            var location = await _context.Locations.FindAsync(request.LocationId);
            if (location == null)
                return ApiResponse<CycleCountResponse>.FailureResponse($"Location {request.LocationId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;
            var countNumber = await _documentNumbers.NextAsync(DocumentType.CycleCount, now);

            var items = new List<CycleCountItem>();
            decimal totalSysVal = 0m;
            decimal totalCountVal = 0m;
            decimal totalVarVal = 0m;

            foreach (var itemDto in request.Items)
            {
                var item = await _context.Items.FindAsync(itemDto.ItemId);
                if (item == null) continue;

                decimal systemQty = 0m;
                decimal unitCost = 0m;

                if (itemDto.LotId.HasValue)
                {
                    var lot = await _context.InventoryLots.FindAsync(itemDto.LotId.Value);
                    if (lot != null)
                    {
                        systemQty = lot.QuantityRemaining;
                        unitCost = lot.UnitCost;
                    }
                }
                else
                {
                    var inv = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.LocationId == request.LocationId && i.ItemId == itemDto.ItemId);
                    systemQty = inv?.CurrentStock ?? 0m;

                    var latestLot = await _context.InventoryLots
                        .Where(l => l.ItemId == itemDto.ItemId && l.LocationId == request.LocationId)
                        .OrderByDescending(l => l.LotId)
                        .FirstOrDefaultAsync();
                    unitCost = latestLot?.UnitCost ?? 0m;
                }

                var varQty = itemDto.CountedQuantity - systemQty;
                var varCost = varQty * unitCost;

                totalSysVal += systemQty * unitCost;
                totalCountVal += itemDto.CountedQuantity * unitCost;
                totalVarVal += varCost;

                items.Add(new CycleCountItem
                {
                    ItemId = itemDto.ItemId,
                    LotId = itemDto.LotId,
                    SystemQuantity = systemQty,
                    CountedQuantity = itemDto.CountedQuantity,
                    UnitCost = unitCost,
                    Reason = itemDto.Reason,
                    Notes = itemDto.Notes
                });
            }

            var cycleCount = new CycleCount
            {
                CountNumber = countNumber,
                LocationId = request.LocationId,
                CountDate = now,
                Status = CycleCountStatus.Completed,
                CountedBy = actor.AuditName,
                Notes = request.Notes,
                TotalSystemValue = totalSysVal,
                TotalCountedValue = totalCountVal,
                TotalVarianceValue = totalVarVal,
                Items = items
            };

            _context.CycleCounts.Add(cycleCount);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(CycleCount),
                cycleCount.CountNumber,
                "CycleCountCreated",
                fieldName: "Status",
                oldValue: null,
                newValue: "Completed");

            var reloaded = await _context.CycleCounts
                .Include(c => c.Location)
                .Include(c => c.Items).ThenInclude(i => i.Item)
                .Include(c => c.Items).ThenInclude(i => i.Lot)
                .FirstAsync(c => c.CycleCountId == cycleCount.CycleCountId);

            return ApiResponse<CycleCountResponse>.SuccessResponse(
                MapToResponse(reloaded),
                $"Cycle count {cycleCount.CountNumber} recorded. Total variance: ₱{totalVarVal:N2}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cycle count");
            return ApiResponse<CycleCountResponse>.FailureResponse($"Failed to create cycle count: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CycleCountResponse>> ReconcileCycleCountAsync(int countId, ReconcileCycleCountRequest? request = null)
    {
        try
        {
            var cc = await _context.CycleCounts
                .Include(c => c.Location)
                .Include(c => c.Items).ThenInclude(i => i.Item)
                .Include(c => c.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(c => c.CycleCountId == countId);

            if (cc == null)
                return ApiResponse<CycleCountResponse>.FailureResponse($"Cycle count {countId} not found.");

            if (cc.Status != CycleCountStatus.Completed)
                return ApiResponse<CycleCountResponse>.FailureResponse($"Cycle count is currently {EnumDbValue.ToDbValue(cc.Status)} and cannot be reconciled.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            await _posting.ExecuteAsync(async () =>
            {
                foreach (var item in cc.Items)
                {
                    var variance = item.VarianceQuantity;
                    if (variance == 0m) continue;

                    // 1. Adjust Lot if specific lot was counted
                    if (item.LotId.HasValue && item.Lot != null)
                    {
                        item.Lot.QuantityRemaining = item.CountedQuantity;
                        _context.InventoryLots.Update(item.Lot);
                    }

                    // 2. Adjust Inventory On-Hand Cache
                    var inv = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.LocationId == cc.LocationId && i.ItemId == item.ItemId);

                    if (inv != null)
                    {
                        inv.CurrentStock = Math.Max(0m, inv.CurrentStock + variance);
                        _context.Inventories.Update(inv);
                    }

                    var lotId = item.LotId;
                    if (!lotId.HasValue)
                    {
                        var existingLot = await _context.InventoryLots
                            .FirstOrDefaultAsync(l => l.ItemId == item.ItemId && l.LocationId == cc.LocationId);
                        if (existingLot != null)
                        {
                            lotId = existingLot.LotId;
                        }
                        else
                        {
                            var newLot = new InventoryLot
                            {
                                LotCode = $"LOT-ADJ-{cc.CountNumber}-{item.ItemId}",
                                ItemId = item.ItemId,
                                LocationId = cc.LocationId,
                                SourceType = LotSourceType.Purchased,
                                QuantityReceived = item.CountedQuantity,
                                QuantityRemaining = item.CountedQuantity,
                                UomId = item.Item?.StockUomId ?? 1,
                                UnitCost = item.UnitCost,
                                Status = LotStatus.Available,
                                ReceivedDate = now
                            };
                            _context.InventoryLots.Add(newLot);
                            await _context.SaveChangesAsync();
                            lotId = newLot.LotId;
                        }
                    }

                    // 3. Append StockLedger adjustment entry
                    _context.StockLedgers.Add(new StockLedger
                    {
                        LotId = lotId.Value,
                        ItemId = item.ItemId,
                        LocationId = cc.LocationId,
                        MovementType = MovementType.Adjustment,
                        Quantity = variance,
                        UomId = item.Item?.StockUomId ?? 1,
                        UnitCost = item.UnitCost,
                        ReferenceType = "CycleCountReconciliation",
                        ReferenceId = cc.CountNumber,
                        UserId = actor.UserId,
                        UserName = actor.AuditName,
                        PostedAt = now,
                        Notes = $"Physical count adjustment for {cc.CountNumber}. Variance: {variance} (Reason: {item.Reason ?? "Physical Audit"})"
                    });
                }

                cc.Status = CycleCountStatus.Reconciled;
                cc.ReconciledDate = now;
                cc.ReconciledBy = actor.AuditName;
                if (!string.IsNullOrWhiteSpace(request?.ReconciliationNotes))
                    cc.Notes = $"{cc.Notes} | Reconciled: {request.ReconciliationNotes}";

                _audit.Record(
                    nameof(CycleCount),
                    cc.CountNumber,
                    "CycleCountReconciled",
                    fieldName: "Status",
                    oldValue: "Completed",
                    newValue: "Reconciled");

                return cc;
            });

            return ApiResponse<CycleCountResponse>.SuccessResponse(
                MapToResponse(cc),
                $"Cycle count {cc.CountNumber} reconciled and inventory balances updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling cycle count {CountId}", countId);
            return ApiResponse<CycleCountResponse>.FailureResponse($"Failed to reconcile cycle count: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CycleCountResponse>> CancelCycleCountAsync(int countId, string reason)
    {
        try
        {
            var cc = await _context.CycleCounts
                .Include(c => c.Location)
                .Include(c => c.Items).ThenInclude(i => i.Item)
                .Include(c => c.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(c => c.CycleCountId == countId);

            if (cc == null)
                return ApiResponse<CycleCountResponse>.FailureResponse($"Cycle count {countId} not found.");

            if (cc.Status == CycleCountStatus.Reconciled)
                return ApiResponse<CycleCountResponse>.FailureResponse("Reconciled cycle counts cannot be cancelled.");

            var actor = _currentUser.Current;
            cc.Status = CycleCountStatus.Cancelled;
            cc.Notes = $"{cc.Notes} | Cancelled: {reason}";

            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(CycleCount),
                cc.CountNumber,
                "CycleCountCancelled",
                fieldName: "Status",
                oldValue: "Completed",
                newValue: "Cancelled");

            return ApiResponse<CycleCountResponse>.SuccessResponse(
                MapToResponse(cc),
                $"Cycle count {cc.CountNumber} cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling cycle count {CountId}", countId);
            return ApiResponse<CycleCountResponse>.FailureResponse($"Failed to cancel cycle count: {ex.Message}");
        }
    }

    private static CycleCountResponse MapToResponse(CycleCount c) => new()
    {
        CycleCountId = c.CycleCountId,
        CountNumber = c.CountNumber,
        LocationId = c.LocationId,
        LocationName = c.Location?.LocationName ?? $"Location {c.LocationId}",
        CountDate = c.CountDate,
        ReconciledDate = c.ReconciledDate,
        Status = EnumDbValue.ToDbValue(c.Status),
        CountedBy = c.CountedBy,
        ReconciledBy = c.ReconciledBy,
        Notes = c.Notes,
        TotalSystemValue = c.TotalSystemValue,
        TotalCountedValue = c.TotalCountedValue,
        TotalVarianceValue = c.TotalVarianceValue,
        Items = c.Items.Select(i => new CycleCountItemResponse
        {
            CycleCountItemId = i.CycleCountItemId,
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            LotId = i.LotId,
            LotCode = i.Lot?.LotCode,
            SystemQuantity = i.SystemQuantity,
            CountedQuantity = i.CountedQuantity,
            VarianceQuantity = i.VarianceQuantity,
            UnitCost = i.UnitCost,
            VarianceCost = i.VarianceCost,
            Reason = i.Reason,
            Notes = i.Notes
        }).ToList()
    };
}
