namespace EMSModelLibrary.Models
{
    public class OrganizerRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? Reason { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }

        public OrganizerRequest()
        {
            RequestedAt = DateTime.UtcNow;
        }
    }
}
