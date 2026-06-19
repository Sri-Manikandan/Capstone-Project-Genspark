using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class BookingItemRequest
    {
        [Range(1, int.MaxValue)]
        public int TicketTypeId { get; set; }

        [Range(1, int.MaxValue)]
        public int SeatId { get; set; }
    }

    public class CreateBookingRequest
    {
        [Range(1, int.MaxValue)]
        public int EventId { get; set; }

        [Required]
        public List<BookingItemRequest> Items { get; set; } = new();
    }

    public class BookingItemDto
    {
        public int Id { get; set; }
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public int SeatId { get; set; }
        public string SeatLabel { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string TicketStatus { get; set; } = string.Empty;
    }

    public class BookingDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string BookingReference { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<BookingItemDto> Items { get; set; } = new();
    }

    public class BookingQueryRequest
    {
        /// <summary>Filter by booking status: Pending | Confirmed | Cancelled | Attended</summary>
        public string? Status { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }

    public class ValidateQrRequest
    {
        [Required]
        public string QrPayload { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int ScannedBy { get; set; }
    }
}
