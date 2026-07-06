namespace EMSModelLibrary.Models
{
    // A single show of an event (movie) on a particular screen at a particular time.
    // The event is the movie; each screening carries its own ticket types and seat
    // availability. Screen maps to the seat Section it uses within the venue.
    public class Screening
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string Screen { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public Screening()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public Screening(int id, int eventId, string screen, DateTime startTime, DateTime endTime, string status)
        {
            Id = id;
            EventId = eventId;
            Screen = screen;
            StartTime = startTime;
            EndTime = endTime;
            Status = status;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
