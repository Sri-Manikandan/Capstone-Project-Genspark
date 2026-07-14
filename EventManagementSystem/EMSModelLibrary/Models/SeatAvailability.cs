namespace EMSModelLibrary.Models
{
    /// <summary>
    /// A venue seat paired with whether it is free for a specific screening. The seat grid
    /// query returns the whole grid — taken seats included — so the layout stays stable and
    /// booked seats render as taken rather than disappearing.
    /// </summary>
    public class SeatAvailability
    {
        public Seat Seat { get; set; } = null!;
        public bool IsAvailable { get; set; }
    }
}
