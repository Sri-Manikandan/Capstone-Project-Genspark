namespace EMSModelLibrary.DTOs
{
    public class ChangeLogDto
    {
        public int Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string EntityKey { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Changes { get; set; } = "{}";
        public int UserId { get; set; }
        public string? UserRole { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChangeLogQueryRequest
    {
        public string? EntityName { get; set; }
        public int? UserId { get; set; }
        public string? Action { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
