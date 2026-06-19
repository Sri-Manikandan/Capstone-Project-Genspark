namespace EMSModelLibrary.Models
{
    public class BookingItem
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int TicketTypeId { get; set; }
        public int SeatId { get; set; }
        public decimal UnitPrice { get; set; }
        public string TicketStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public BookingItem()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public BookingItem(int id, int bookingId, int ticketTypeId, int seatId, decimal unitPrice, string ticketStatus)
        {
            Id = id;
            BookingId = bookingId;
            TicketTypeId = ticketTypeId;
            SeatId = seatId;
            UnitPrice = unitPrice;
            TicketStatus = ticketStatus;
            CreatedAt = DateTime.UtcNow;
        }
    }
}