using System;
using System.Collections.Generic;

namespace DistributionSystem.Business.Dtos
{
    public class DailyReportDto
    {
        public DateTime ReportDate { get; set; }

        public string GeneratedAt { get; set; } = string.Empty;

        // ── Sales ─────────────────────────────
        public List<DailyInvoiceRow> Invoices { get; set; }
            = new List<DailyInvoiceRow>();

        // ── Inbounds ──────────────────────────
        public List<DailyInboundRow> Inbounds { get; set; }
            = new List<DailyInboundRow>();

        // ── Returns ───────────────────────────
        public List<DailyReturnRow> Returns { get; set; }
            = new List<DailyReturnRow>();

        // ── Payments ──────────────────────────
        public List<DailyPaymentRow> Payments { get; set; }
            = new List<DailyPaymentRow>();

        // ── Expenses ──────────────────────────
        public List<DailyExpenseRow> Expenses { get; set; }
            = new List<DailyExpenseRow>();

        // ── Activity Logs ─────────────────────
        public List<ActivityLogDto> ActivityLogs { get; set; }
            = new List<ActivityLogDto>();

        // ── Summary ───────────────────────────
        public decimal TotalSalesRevenue { get; set; }

        public decimal TotalPaidToday { get; set; }

        public decimal TotalInboundCost { get; set; }

        public decimal TotalExpenses { get; set; }

        public decimal NetProfit { get; set; }

        public decimal TreasuryBalance { get; set; }
    }
}