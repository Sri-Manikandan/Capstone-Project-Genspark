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

    // Rich payload for the admin approval queue: the event plus everything an admin needs
    // to decide — who the organizer is, their track record, ticketing, and computed signals.
    public class PendingEventReviewDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Screen { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? RejectionReason { get; set; }

        public int VenueId { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        public OrganizerSummaryDto Organizer { get; set; } = new();
        public List<TicketCategorySummaryDto> TicketCategories { get; set; } = new();
        public ReviewSignalsDto Signals { get; set; } = new();
    }

    public class OrganizerSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime MemberSince { get; set; }
        public bool IsActive { get; set; }
        public int PublishedEventCount { get; set; }
        public int RejectedEventCount { get; set; }
        public int TotalEventCount { get; set; }
    }

    public class TicketCategorySummaryDto
    {
        public string Name { get; set; } = string.Empty;
        public string SeatType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalQuantity { get; set; }
    }

    public class ReviewSignalsDto
    {
        public bool LeadTimeOk { get; set; }
        public bool ImageUrlValid { get; set; }
        public bool DescriptionAdequate { get; set; }
        public bool HasTicketCategories { get; set; }
        public bool PricingSane { get; set; }
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
