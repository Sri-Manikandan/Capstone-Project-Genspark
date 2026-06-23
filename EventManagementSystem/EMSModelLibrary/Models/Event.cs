namespace EMSModelLibrary.Models
{
    public class Event
    {
        public int Id { get; set; }
        public int OrganizerId { get; set; }
        public int VenueId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public string Screen { get; set; } = string.Empty;

        public Event()
        {
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public Event(int id, int organizerId, int venueId, string title, string description, string status, DateTime startTime, DateTime endTime, string imageUrl, string category, string slug)
        {
            Id = id;
            OrganizerId = organizerId;
            VenueId = venueId;
            Title = title;
            Description = description;
            Status = status;
            StartTime = startTime;
            EndTime = endTime;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            ImageUrl = imageUrl;
            Category = category;
            Slug = slug;
        }
    }
}