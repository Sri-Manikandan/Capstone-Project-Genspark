using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class OrganizerRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ApplicantReason { get; set; }
        public string? Reason { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }
    }

    public class RequestOrganizerRoleRequest
    {
        [Required]
        [StringLength(500, MinimumLength = 10)]
        public string Reason { get; set; } = string.Empty;
    }

    public class ReviewOrganizerRequestRequest
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public class ReviewEventRequest
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public class SeedResetRequest
    {
        /// <summary>Must be true — a guard against accidentally wiping all demo data.</summary>
        public bool Confirm { get; set; }
    }

    public class OrganizerRequestQueryRequest
    {
        /// <summary>Filter by status: Pending | Approved | Rejected</summary>
        public string? Status { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
}
