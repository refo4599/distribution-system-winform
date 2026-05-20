using System;
namespace DistributionSystem.Business.Dtos
{
    public class EmployeeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal TotalLoans { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty; // ? «·„”„Ï «·ÊŸÌ›Ì
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}