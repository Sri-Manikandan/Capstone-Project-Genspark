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
        public string? Reason { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }
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
