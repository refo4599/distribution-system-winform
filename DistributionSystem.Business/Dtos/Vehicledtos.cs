using System;
using System.Collections.Generic;

namespace DistributionSystem.Business.Dtos
{
    // ── Vehicle ───────────────────────────────────────────────
    public class VehicleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string RepName { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    // ── Dispatch Order ────────────────────────────────────────
    public class DispatchOrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal SalePrice { get; set; }
        public int BoxesPerCarton { get; set; } = 1;

        /// <summary>
        /// الكمية الأصلية وقت إنشاء أمر الصرف — لا تتغير بعد الإرجاع
        /// تُستخدم في ShowDetailsPopup لحساب المباع والمتبقي بشكل صحيح
        /// </summary>
        public int OriginalQuantity { get; set; }
    }

    public class DispatchOrderDto
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string VehicleName { get; set; } = "";
        public string RepName { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<DispatchOrderItemDto> Items { get; set; } = new List<DispatchOrderItemDto>();
    }

    // ── Return Order ──────────────────────────────────────────
    public class ReturnOrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class ReturnOrderDto
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string VehicleName { get; set; } = "";
        public string RepName { get; set; } = "";
        public string Notes { get; set; } = "";
        public int? DispatchOrderId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ReturnOrderItemDto> Items { get; set; } = new List<ReturnOrderItemDto>();
    }

    // ── Sales Invoice Item ────────────────────────────────────
    public class SalesInvoiceItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public decimal TotalPrice => Quantity * SalePrice;

        /// <summary>
        /// عدد العلب في الكرتونة — محفوظ وقت إنشاء الفاتورة
        /// يُستخدم في التقارير لعرض "X كرتون + Y علبة"
        /// </summary>
        public int BoxesPerCarton { get; set; } = 1;
    }

    // ── Sales Invoice ─────────────────────────────────────────
    public class SalesInvoiceDto
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public string VehicleName { get; set; } = "";
        public string RepName { get; set; } = "";
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Remaining => TotalAmount - PaidAmount;
        public string PaymentType { get; set; } = "Cash";
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<SalesInvoiceItemDto> Items { get; set; } = new List<SalesInvoiceItemDto>();
    }

    // ── Invoice Payment ───────────────────────────────────────
    public class InvoicePaymentDto
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}