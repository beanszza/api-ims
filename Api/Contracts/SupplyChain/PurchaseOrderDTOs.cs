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
        public int PoItemQuantity { get; set; }
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
    }
}

namespace api_scm.Contracts.Responses
{
    public class PurchaseOrderResponse
    {
        public int PoId { get; set; }
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
        public int PoItemQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
    }

    public class TransactionHistoryResponse
    {
        public int MovementId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public int ChangeQuantity { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string ReferenceId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
