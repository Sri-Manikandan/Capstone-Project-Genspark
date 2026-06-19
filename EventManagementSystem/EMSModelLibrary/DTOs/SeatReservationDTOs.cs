using System.ComponentModel.DataAnnotations;

namespace EMSModelLibrary.DTOs
{
    public class ReserveSeatRequest
    {
        [Range(1, int.MaxValue)]
        public int EventId { get; set; }

        [Range(1, int.MaxValue)]
        public int SeatId { get; set; }

        [Range(1, int.MaxValue)]
        public int TicketTypeId { get; set; }
    }

    public class SeatReservationDto
    {
        public int Id { get; set; }
        public int SeatId { get; set; }
        public int EventId { get; set; }
        public int TicketTypeId { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ReservedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
