namespace EMSBLLLibrary.Interfaces
{
    public interface ISeatNotifier
    {
        Task SeatReserved(int eventId, int seatId);
        Task SeatReleased(int eventId, int seatId);
        Task SeatBooked(int eventId, int seatId);
    }
}
