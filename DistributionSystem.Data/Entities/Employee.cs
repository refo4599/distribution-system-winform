using System;
namespace DistributionSystem.Data.Entities
{
    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public decimal RemainingBalance { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty; // ? «·„”„Ï «·ÊŸÌ›Ì
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}