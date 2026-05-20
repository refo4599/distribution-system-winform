// =========================
// ActivityLogDto.cs
// DistributionSystem.Business.Dtos
// =========================

using System;

namespace DistributionSystem.Business.Dtos
{
    public class ActivityLogDto
    {
        public int Id { get; set; }

        public string ActionType { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public int? ReferenceId { get; set; }

        public string Description { get; set; } = string.Empty;

        public string BeforeData { get; set; } = string.Empty;

        public string AfterData { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}