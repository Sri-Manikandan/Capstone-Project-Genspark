namespace EMSModelLibrary.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public RefreshToken()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public RefreshToken(int id, int userId, string token, DateTime expiresAt)
        {
            Id = id;
            UserId = userId;
            Token = token;
            ExpiresAt = expiresAt;
            RevokedAt = null;
            CreatedAt = DateTime.UtcNow;
        }
    }
}