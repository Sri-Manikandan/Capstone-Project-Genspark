using AutoMapper;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Contexts;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSBLLLibrary.Services
{
    public class SeatReservationService : ISeatReservationService
    {
        private readonly ISeatReservationRepository _reservationRepo;
        private readonly IScreeningRepository _screeningRepo;
        private readonly IEventRepository _eventRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly ITicketTypeRepository _ticketTypeRepo;
        private readonly ISeatNotifier _notifier;
        private readonly EventContext _context;
        private readonly IMapper _mapper;

        private static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(10);

        public SeatReservationService(
            ISeatReservationRepository reservationRepo,
            IScreeningRepository screeningRepo,
            IEventRepository eventRepo,
            ISeatRepository seatRepo,
            ITicketTypeRepository ticketTypeRepo,
            ISeatNotifier notifier,
            EventContext context,
            IMapper mapper)
        {
            _reservationRepo = reservationRepo;
            _screeningRepo = screeningRepo;
            _eventRepo = eventRepo;
            _seatRepo = seatRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _notifier = notifier;
            _context = context;
            _mapper = mapper;
        }

        public async Task<SeatReservationDto> Reserve(int userId, ReserveSeatRequest request)
        {
            var screening = await _screeningRepo.GetById(request.ScreeningId)
                ?? throw new NotFoundException($"Screening {request.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.Status != "Published")
                throw new ValidationException("Reservations are only allowed for published events.");

            if (screening.StartTime <= DateTime.UtcNow)
                throw new ValidationException("Reservations are not allowed for screenings that have already started or ended.");

            var seat = await _seatRepo.GetById(request.SeatId)
                ?? throw new NotFoundException($"Seat {request.SeatId} not found.");

            var ticketType = await _ticketTypeRepo.GetById(request.TicketTypeId)
                ?? throw new NotFoundException($"TicketType {request.TicketTypeId} not found.");

            if (ticketType.ScreeningId != request.ScreeningId)
                throw new ValidationException("The selected ticket type does not belong to this screening.");

            if (!string.Equals(ticketType.SeatType, seat.SeatType, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException($"Seat type mismatch: seat is '{seat.SeatType}' but ticket type is '{ticketType.SeatType}'.");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                var existingReservation = await _context.SeatReservations
                    .FirstOrDefaultAsync(sr => sr.ScreeningId == request.ScreeningId
                                            && sr.SeatId == request.SeatId
                                            && sr.Status == "Active"
                                            && sr.ReservedUntil > now);

                if (existingReservation != null)
                    throw new ValidationException("This seat is already reserved by another user.");

                var alreadyBooked = await _context.BookingItems
                    .Join(_context.Bookings,
                        bi => bi.BookingId,
                        b => b.Id,
                        (bi, b) => new { bi.SeatId, b.ScreeningId, b.BookingStatus })
                    .AnyAsync(x => x.ScreeningId == request.ScreeningId
                                && x.SeatId == request.SeatId
                                && x.BookingStatus != "Cancelled");

                if (alreadyBooked)
                    throw new ValidationException("This seat is already booked.");

                var reservation = new SeatReservation
                {
                    SeatId = request.SeatId,
                    TicketTypeId = request.TicketTypeId,
                    ScreeningId = request.ScreeningId,
                    UserId = userId,
                    Status = "Active",
                    ReservedUntil = now.Add(ReservationTtl)
                };

                _context.SeatReservations.Add(reservation);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _notifier.SeatReserved(request.ScreeningId, request.SeatId);

                return _mapper.Map<SeatReservationDto>(reservation);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task Release(int reservationId, int userId)
        {
            var reservation = await _reservationRepo.GetById(reservationId)
                ?? throw new NotFoundException($"Reservation {reservationId} not found.");

            if (reservation.UserId != userId)
                throw new UnauthorizedException("Not authorized to release this reservation.");

            if (reservation.Status != "Active")
                throw new ValidationException("Reservation is not active.");

            reservation.Status = "Released";
            await _reservationRepo.Update(reservation);

            await _notifier.SeatReleased(reservation.ScreeningId, reservation.SeatId);
        }
    }
}
