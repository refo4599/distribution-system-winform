using System;

namespace DistributionSystem.Business.Dtos
{
    // ════════════════════════════════════════
    // Invoice
    // ════════════════════════════════════════

    public class DailyInvoiceRow
    {
        public int Id { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public decimal Paid { get; set; }

        public decimal Remaining { get; set; }

        public string PaymentType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════
    // Inbound
    // ════════════════════════════════════════

    public class DailyInboundRow
    {
        public int Id { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal PurchasePrice { get; set; }

        public decimal TotalCost { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════
    // Return
    // ════════════════════════════════════════

    public class DailyReturnRow
    {
        public int Id { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════
    // Payment
    // ════════════════════════════════════════

    public class DailyPaymentRow
    {
        public int InvoiceId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    // ════════════════════════════════════════
    // Expense
    // ════════════════════════════════════════

    public class DailyExpenseRow
    {
        public string Type { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}