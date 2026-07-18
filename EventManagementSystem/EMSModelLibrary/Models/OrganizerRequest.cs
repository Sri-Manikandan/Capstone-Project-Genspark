namespace EMSModelLibrary.Models
{
    public class OrganizerRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? ApplicantReason { get; set; } // Why the user wants to become an organizer
        public string? Reason { get; set; } // Admin's review/rejection note
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }

        public OrganizerRequest()
        {
            RequestedAt = DateTime.UtcNow;
        }
    }
}
