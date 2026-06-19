namespace EMSModelLibrary.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public string QrPayload { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public DateTime? ScannedAt { get; set; }
        public int? ScannedBy { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public Booking()
        {
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public Booking(int id, int userId, int eventId, string bookingReference, string qrCode, string qrPayload, decimal totalAmount, string bookingStatus, DateTime expiresAt)
        {
            Id = id;
            UserId = userId;
            EventId = eventId;
            BookingReference = bookingReference;
            QrCode = qrCode;
            QrPayload = qrPayload;
            TotalAmount = totalAmount;
            BookingStatus = bookingStatus;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}