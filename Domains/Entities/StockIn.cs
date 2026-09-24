using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class StockIn
{
    public int StockInId { get; set; }

    /// <summary>Canonical human-readable Stock-In number, e.g. "SIN-2026-0001".</summary>
    public string StockInNumber { get; set; } = string.Empty;

    public int GrnId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public StockInStatus Status { get; set; } = StockInStatus.Draft;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    public string? CommittedBy { get; set; }
    public DateTime? CommittedAt { get; set; }

    public string? Notes { get; set; }

    public ICollection<StockInLine> Lines { get; set; } = new List<StockInLine>();
}
