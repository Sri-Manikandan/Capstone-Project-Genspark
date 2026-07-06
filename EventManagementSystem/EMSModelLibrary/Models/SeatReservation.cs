namespace EMSModelLibrary.Models
{
    public class SeatReservation
    {
        public int Id { get; set; }
        public int SeatId { get; set; }
        public int TicketTypeId { get; set; }
        public int ScreeningId { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ReservedUntil { get; set; }

        public SeatReservation()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public SeatReservation(int id, int seatId, int ticketTypeId, int screeningId, int userId, string status, DateTime reservedUntil)
        {
            Id = id;
            SeatId = seatId;
            TicketTypeId = ticketTypeId;
            ScreeningId = screeningId;
            UserId = userId;
            Status = status;
            CreatedAt = DateTime.UtcNow;
            ReservedUntil = reservedUntil;
        }
    }
}