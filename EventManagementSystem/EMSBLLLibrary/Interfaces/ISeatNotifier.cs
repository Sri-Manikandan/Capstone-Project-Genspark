namespace EMSBLLLibrary.Interfaces
{
    public interface ISeatNotifier
    {
        Task SeatReserved(int screeningId, int seatId);
        Task SeatReleased(int screeningId, int seatId);
        Task SeatBooked(int screeningId, int seatId);
    }
}
