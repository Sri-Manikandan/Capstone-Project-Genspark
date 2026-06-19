namespace EMSModelLibrary.Models
{
    public class Seat
    {
        public int Id { get; set; }
        public int VenueId { get; set; }
        public string Section { get; set; } = string.Empty;
        public string Row { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public string SeatType { get; set; } = string.Empty;

        public Seat()
        {
            
        }

        public Seat(int id, int venueId, string section, string row, int seatNumber, string seatType)
        {
            Id = id;
            VenueId = venueId;
            Section = section;
            Row = row;
            SeatNumber = seatNumber;
            SeatType = seatType;

        }
    }
}