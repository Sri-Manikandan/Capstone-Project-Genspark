using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class EventDto
    {
        public int Id { get; set; }
        public int OrganizerId { get; set; }
        public int VenueId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Screen { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateEventRequest
    {
        [Range(1, int.MaxValue)]
        public int VenueId { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        [Url]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Category { get; set; } = string.Empty;

        public string Screen { get; set; } = string.Empty;
    }

    public class UpdateEventRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        [Url]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Category { get; set; } = string.Empty;

        public string Screen { get; set; } = string.Empty;
    }

    public class EventSearchRequest
    {
        public string? Query { get; set; }
        public string? Category { get; set; }
        public string? City { get; set; }
        public string? Status { get; set; }
        public DateTime? StartFrom { get; set; }
        public DateTime? StartTo { get; set; }

        /// <summary>Accepted values: title | startTime | createdAt</summary>
        public string? SortBy { get; set; }

        /// <summary>Accepted values: asc | desc (default: desc)</summary>
        public string? SortOrder { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }

    public class MyEventsRequest
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}
