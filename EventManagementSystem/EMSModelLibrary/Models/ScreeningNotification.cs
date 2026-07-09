namespace EMSModelLibrary.Models
{
    public class ScreeningNotification
    {
        public int Id { get; set; }
        public int ScreeningId { get; set; }
        public int UserId { get; set; }
        public int NotificationCount { get; set; }
        public string Status { get; set; } = "Active"; // Active, Completed
        public DateTime CreatedAt { get; set; }

        public ScreeningNotification()
        {
            CreatedAt = DateTime.UtcNow;
            NotificationCount = 0;
            Status = "Active";
        }
    }
}
