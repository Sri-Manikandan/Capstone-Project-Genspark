namespace EMSModelLibrary.Models
{
    public class ChangeLog
    {
        public int Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string EntityKey { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Changes { get; set; } = "{}";
        public int UserId { get; set; }
        public string? UserRole { get; set; }
        public DateTime CreatedAt { get; set; }

        public ChangeLog()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }
}
