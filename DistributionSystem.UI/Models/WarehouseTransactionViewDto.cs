using System;

namespace DistributionSystem.UI.Models
{
    public class WarehouseTransactionViewDto
    {
        public int Id { get; set; }
        public string ProductName { get; set; }
        public string TransactionType { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
