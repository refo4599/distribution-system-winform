using System;

namespace DistributionSystem.Data.Dtos
{
    public class WarehouseBalanceDataDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Balance { get; set; }
        public decimal AvgCost { get; set; }
        public decimal TotalCost { get; set; }
    }
}
