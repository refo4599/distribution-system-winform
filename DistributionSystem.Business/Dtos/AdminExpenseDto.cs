using System;

namespace DistributionSystem.Business.Dtos
{
    public class AdminExpenseDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;   // »‰œ «·„’—Ê›
        public decimal Amount { get; set; }                    // «·≈Ã„«·Ì
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}