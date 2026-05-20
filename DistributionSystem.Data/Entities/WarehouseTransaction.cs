using System;

namespace DistributionSystem.Data.Entities
{
    public class WarehouseTransaction
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        // Inbound, CarLoad, Return, Outbound, CarReturn,
        // SaleRevenue, OpeningBalance, Ê«—œ, —’Ìœ «›  «ÕÌ,
        // EmployeeExpense, AdminExpense, CashDeposit, CashWithdraw
        public string TransactionType { get; set; } = string.Empty;

        public int? ReferenceId { get; set; }

        // Existing column in DB ó keep for compatibility
        public decimal? PurchasePrice { get; set; }

        // Convenience alias
        public decimal? UnitCost
        {
            get => PurchasePrice;
            set => PurchasePrice = value;
        }

        // ?  ÊﬁÌ  «·ÃÂ«“ «·Õ«·Ì »œ· UTC
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string CreatedBy { get; set; } = string.Empty;
    }
}