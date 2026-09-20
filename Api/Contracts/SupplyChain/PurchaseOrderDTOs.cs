using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Requests
{
    public class CreatePurchaseOrderRequest
    {
        /// <summary>Optional reference to the Purchase Requisition this PO is based on.</summary>
        public int? PrId { get; set; }

        public int SupplierId { get; set; }
        public DateTime ExpectedArrivalDate { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        /// <summary>Name of the person creating this PO (from auth context).</summary>
        public string RequestedBy { get; set; } = string.Empty;

        /// <summary>
        /// Initial status: "Draft" (save) or "Pending Approval" (submit for review).
        /// Defaults to Draft if not provided.
        /// </summary>
        public string? InitialStatus { get; set; }

        public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
    }

    public class CreatePurchaseOrderItemRequest
    {
        public int ItemId { get; set; }

        /// <summary>Whole-number quantity to order.</summary>
        public decimal PoItemQuantity { get; set; }

        /// <summary>Total price for this line (user-typed, not per-unit).</summary>
        public decimal TotalPrice { get; set; }

        public int? PurchaseUomId { get; set; }
    }

    /// <summary>How many units of a PR item have already been covered by existing POs.</summary>
    public class PrItemOrderedQtyResponse
    {
        public int ItemId { get; set; }
        public decimal OrderedQty { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdatePurchaseOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;

        /// <summary>Required for Rejected and Returned statuses.</summary>
        public string? AdminNotes { get; set; }
    }

    public class UpdatePurchaseOrderQaRequest : UpdateOrderStatusRequest
    {
        public string? QaNotes { get; set; }
        public string? QaStatus { get; set; }
        public string? InspectedBy { get; set; }

        /// <summary>Admin notes for reject/return reason (repurposed for approval workflow).</summary>
        public string? AdminNotes { get; set; }

        /// <summary>
        /// Where the goods are being received. Optional: omitting it uses the designated system
        /// receiving warehouse rather than an arbitrary location.
        /// </summary>
        public int? ReceivingLocationId { get; set; }
    }
}

namespace api_scm.Contracts.Responses
{
    public class PurchaseOrderResponse
    {
        public int PoId { get; set; }

        /// <summary>Human-readable document number, for example PO-2026-0042.</summary>
        public string PoNumber { get; set; } = string.Empty;

        /// <summary>Reference PR ID if this PO was created from a Purchase Requisition.</summary>
        public int? PrId { get; set; }

        /// <summary>Reference PR number (e.g. PR-2026-0001) for display.</summary>
        public string? PrNumber { get; set; }

        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime ExpectedArrivalDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public string ProofImageUrl { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }

        public string RequestedBy { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }

        public string? QaNotes { get; set; }
        public DateTime? QaInspectedDate { get; set; }
        public string? QaStatus { get; set; }
        public string? InspectedBy { get; set; }

        public List<PurchaseOrderItemResponse> Items { get; set; } = new();
    }

    public class PurchaseOrderItemResponse
    {
        public int PoItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal PoItemQuantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        /// <summary>Total price for this line (user-typed).</summary>
        public decimal TotalPrice { get; set; }
        /// <summary>Derived unit price for backward compat.</summary>
        public decimal UnitPrice => PoItemQuantity > 0 ? TotalPrice / PoItemQuantity : 0;
        public int PurchaseUomId { get; set; }
        public string PurchaseUomName { get; set; } = string.Empty;
        public decimal LineTotal => TotalPrice;
    }

    public class TransactionHistoryResponse
    {
        public int MovementId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public decimal ChangeQuantity { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;

        /// <summary>Auth subject or system sentinel. A string because identity is owned by the auth service.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Display name captured when the movement was posted.</summary>
        public string UserName { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }
    }
}
