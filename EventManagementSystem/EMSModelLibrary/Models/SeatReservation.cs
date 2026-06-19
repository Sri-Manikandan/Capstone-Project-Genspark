namespace EMSModelLibrary.Models
{
    public class SeatReservation
    {
        public int Id { get; set; }
        public int SeatId { get; set; }
        public int TicketTypeId { get; set; }
        public int EventId { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ReservedUntil { get; set; }

        public SeatReservation()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public SeatReservation(int id, int seatId, int ticketTypeId, int eventId, int userId, string status, DateTime reservedUntil)
        {
            Id = id;
            SeatId = seatId;
            TicketTypeId = ticketTypeId;
            EventId = eventId;
            UserId = userId;
            Status = status;
            CreatedAt = DateTime.UtcNow;
            ReservedUntil = reservedUntil;
        }
    }
}