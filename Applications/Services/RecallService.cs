using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

public class RecallService : IRecallService
{
    private readonly ScmDbContext _context;
    private readonly ITraceabilityService _traceability;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<RecallService> _logger;

    public RecallService(
        ScmDbContext context,
        ITraceabilityService traceability,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<RecallService> logger)
    {
        _context = context;
        _traceability = traceability;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<RecallSimulationResponse>> SimulateRecallAsync(SimulateRecallRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LotCode))
                return ApiResponse<RecallSimulationResponse>.FailureResponse("Target lot code is required.");

            var traceResult = await _traceability.TraceForwardAsync(request.LotCode.Trim());
            if (!traceResult.Success || traceResult.Data == null)
            {
                return ApiResponse<RecallSimulationResponse>.FailureResponse($"Failed to trace lot {request.LotCode}: {traceResult.Message}");
            }

            var trace = traceResult.Data;
            var now = DateTime.UtcNow;
            var actor = _currentUser.Current;

            var recallNumber = $"REC-{now:yyyy}-{Guid.NewGuid():N}"[..13].ToUpperInvariant();

            var affectedBatchesCount = trace.AffectedProductionBatches.Count;
            var affectedFgLots = trace.AffectedFinishedGoodsLots;
            var affectedBranches = trace.DownstreamBranchShipments
                .Select(s => s.DestinationBranchName)
                .Distinct()
                .ToList();

            decimal totalQtyAtRisk = affectedFgLots.Sum(fg => fg.QuantityRemaining);
            decimal totalLoss = 0m;

            var fgLotIds = affectedFgLots.Select(fg => fg.LotId).ToList();
            var realFgLots = await _context.InventoryLots
                .Where(l => fgLotIds.Contains(l.LotId))
                .ToListAsync();

            totalLoss = realFgLots.Sum(l => l.QuantityRemaining * l.UnitCost);

            string actionTaken = request.ApplyQuarantineHold
                ? $"Active Hold: {realFgLots.Count} downstream finished goods lots placed on Quarantine hold immediately."
                : "Simulation Only: No stock status changes committed.";

            if (request.ApplyQuarantineHold && realFgLots.Any())
            {
                await _posting.ExecuteAsync(async () =>
                {
                    foreach (var fgLot in realFgLots)
                    {
                        if (fgLot.Status == LotStatus.Available)
                        {
                            var qtyToHold = fgLot.QuantityRemaining;
                            fgLot.Status = LotStatus.Quarantine;
                            _context.InventoryLots.Update(fgLot);

                            // Debit from available inventory cache
                            var inv = await _context.Inventories
                                .FirstOrDefaultAsync(i => i.ItemId == fgLot.ItemId && i.LocationId == fgLot.LocationId);

                            if (inv != null)
                            {
                                inv.CurrentStock = Math.Max(0m, inv.CurrentStock - qtyToHold);
                                _context.Inventories.Update(inv);
                            }

                            _context.StockLedgers.Add(new StockLedger
                            {
                                LotId = fgLot.LotId,
                                ItemId = fgLot.ItemId,
                                LocationId = fgLot.LocationId,
                                MovementType = MovementType.Adjustment,
                                Quantity = -qtyToHold,
                                UomId = fgLot.UomId,
                                UnitCost = fgLot.UnitCost,
                                ReferenceType = "RecallQuarantineHold",
                                ReferenceId = recallNumber,
                                UserId = actor.UserId,
                                UserName = actor.AuditName,
                                PostedAt = now,
                                Notes = $"Quarantine hold triggered by Recall {recallNumber} (Target Lot: {request.LotCode})"
                            });
                        }
                    }

                    _audit.Record(
                        nameof(RecallRecord),
                        recallNumber,
                        "QuarantineHoldCascaded",
                        fieldName: "Status",
                        oldValue: "Available",
                        newValue: "Quarantine");

                    return realFgLots;
                });
            }

            var record = new RecallRecord
            {
                RecallNumber = recallNumber,
                TargetLotCode = request.LotCode.Trim(),
                Scope = trace.SourceLot.SourceType == LotSourceType.Produced.ToString() ? "FinishedGood" : "RawMaterial",
                Reason = request.Reason,
                Status = request.ApplyQuarantineHold ? "ActiveHold" : "Simulated",
                InitiatedAt = now,
                CompletedAt = now,
                InitiatedBy = actor.AuditName,
                TotalUnitsAffected = totalQtyAtRisk,
                BranchesAffectedCount = affectedBranches.Count,
                AffectedSummaryJson = JsonSerializer.Serialize(new
                {
                    Batches = trace.AffectedProductionBatches.Select(b => b.BatchNumber),
                    FinishedGoods = trace.AffectedFinishedGoodsLots.Select(fg => fg.LotCode),
                    Branches = affectedBranches
                }),
                Notes = request.Notes
            };

            _context.RecallRecords.Add(record);
            await _context.SaveChangesAsync();

            var response = new RecallSimulationResponse
            {
                RecallNumber = recallNumber,
                TargetLotCode = request.LotCode.Trim(),
                Reason = request.Reason,
                SimulatedAt = now,
                AffectedBatchesCount = affectedBatchesCount,
                AffectedFinishedGoodsLotsCount = affectedFgLots.Count,
                TotalFinishedGoodsQuantityAtRisk = totalQtyAtRisk,
                AffectedBranchesCount = affectedBranches.Count,
                AffectedBranchNames = affectedBranches,
                TotalEstimatedLoss = totalLoss,
                QuarantineHoldApplied = request.ApplyQuarantineHold,
                ActionSummary = actionTaken
            };

            return ApiResponse<RecallSimulationResponse>.SuccessResponse(
                response,
                $"Recall simulation {recallNumber} completed. {affectedBatchesCount} batches and {affectedFgLots.Count} FG lots traced (₱{totalLoss:N2} estimated exposure).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating recall for lot {LotCode}", request.LotCode);
            return ApiResponse<RecallSimulationResponse>.FailureResponse($"Recall simulation failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<RecallRecord>>> GetRecallHistoryAsync()
    {
        try
        {
            var list = await _context.RecallRecords.OrderByDescending(r => r.RecallId).ToListAsync();
            return ApiResponse<List<RecallRecord>>.SuccessResponse(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recall records");
            return ApiResponse<List<RecallRecord>>.FailureResponse("An error occurred while fetching recall records.");
        }
    }
}
