using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Requests
{
    public class CreatePurchaseOrderRequest
    {
        public int SupplierId { get; set; }
        public DateTime ExpectedArrivalDate { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
    }

    public class CreatePurchaseOrderItemRequest
    {
        public int ItemId { get; set; }
        public decimal PoItemQuantity { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdatePurchaseOrderQaRequest : UpdateOrderStatusRequest
    {
        public string? QaNotes { get; set; }
        public string? QaStatus { get; set; }
        public string? InspectedBy { get; set; }

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

        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime ExpectedArrivalDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public string ProofImageUrl { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        
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
