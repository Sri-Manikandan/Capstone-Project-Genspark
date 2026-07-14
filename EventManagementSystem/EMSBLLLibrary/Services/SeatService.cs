using AutoMapper;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class SeatService : ISeatService
    {
        private const int MaxSeatTypeLength = 50;

        private readonly ISeatRepository _seatRepo;
        private readonly IVenueRepository _venueRepo;
        private readonly IMapper _mapper;

        public SeatService(ISeatRepository seatRepo, IVenueRepository venueRepo, IMapper mapper)
        {
            _seatRepo = seatRepo;
            _venueRepo = venueRepo;
            _mapper = mapper;
        }

        public async Task<SeatDto> Create(CreateSeatRequest request)
        {
            if (request.VenueId <= 0)
                throw new ValidationException("VenueId must be greater than zero.");
            if (string.IsNullOrWhiteSpace(request.Section))
                throw new ValidationException("Section is required.");
            if (string.IsNullOrWhiteSpace(request.Row))
                throw new ValidationException("Row is required.");
            if (request.SeatNumber <= 0)
                throw new ValidationException("SeatNumber must be greater than zero.");
            if (string.IsNullOrWhiteSpace(request.SeatType))
                throw new ValidationException("SeatType is required.");

            var seat = new Seat
            {
                VenueId = request.VenueId,
                Section = request.Section,
                Row = request.Row,
                SeatNumber = request.SeatNumber,
                SeatType = request.SeatType
            };
            await _seatRepo.Add(seat);
            return _mapper.Map<SeatDto>(seat);
        }

        public async Task<List<SeatDto>> BulkCreate(BulkCreateSeatsRequest request)
        {
            if (request.VenueId <= 0)
                throw new ValidationException("VenueId must be greater than zero.");
            if (string.IsNullOrWhiteSpace(request.Section))
                throw new ValidationException("Section is required.");
            if (string.IsNullOrWhiteSpace(request.Row))
                throw new ValidationException("Row is required.");
            if (string.IsNullOrWhiteSpace(request.SeatType))
                throw new ValidationException("SeatType is required.");
            if (request.StartNumber <= 0)
                throw new ValidationException("StartNumber must be greater than zero.");
            if (request.EndNumber < request.StartNumber)
                throw new ValidationException("EndNumber must be greater than or equal to StartNumber.");
            if (request.EndNumber - request.StartNumber + 1 > 1000)
                throw new ValidationException("Cannot create more than 1000 seats in a single bulk operation.");

            // Bulk create adds to a section rather than replacing it, so every existing seat counts.
            await EnsureWithinVenueCapacity(request.VenueId, null, request.EndNumber - request.StartNumber + 1);

            var seats = new List<Seat>();
            for (int num = request.StartNumber; num <= request.EndNumber; num++)
            {
                var seat = new Seat
                {
                    VenueId = request.VenueId,
                    Section = request.Section,
                    Row = request.Row,
                    SeatNumber = num,
                    SeatType = request.SeatType
                };
                await _seatRepo.Add(seat);
                seats.Add(seat);
            }
            return _mapper.Map<List<SeatDto>>(seats);
        }

        public async Task<List<SeatDto>> GetByVenueId(int venueId)
        {
            var seats = await _seatRepo.GetByVenueId(venueId);
            return _mapper.Map<List<SeatDto>>(seats);
        }

        public async Task<List<SeatDto>> GetAvailableByScreeningId(int screeningId)
        {
            var seats = await _seatRepo.GetAvailableByScreeningId(screeningId);
            return _mapper.Map<List<SeatDto>>(seats);
        }

        public async Task Delete(int id)
        {
            var seat = await _seatRepo.GetById(id)
                ?? throw new NotFoundException($"Seat {id} not found.");
            await _seatRepo.Delete(seat.Id);
        }

        public async Task<List<SeatDto>> SetScreenSeats(SetScreenSeatsRequest request)
        {
            if (request.VenueId <= 0)
                throw new ValidationException("VenueId must be greater than zero.");
            if (string.IsNullOrWhiteSpace(request.Screen))
                throw new ValidationException("Screen is required.");
            if (request.Seats == null || request.Seats.Count == 0)
                throw new ValidationException("At least one seat is required.");

            if (await _seatRepo.ScreenHasActiveSeatUsage(request.VenueId, request.Screen))
                throw new ValidationException("Cannot edit a screen that already has bookings.");

            // This screen replaces its own seats, so it is the other screens' seats that
            // this one has to fit alongside inside the venue's declared capacity.
            await EnsureWithinVenueCapacity(request.VenueId, request.Screen, request.Seats.Count);

            var retained = (await _seatRepo.GetByVenueId(request.VenueId))
                .Where(s => s.Section != request.Screen)
                .ToList();
            EnsureSeatTypesAreConsistent(request.Seats, retained);

            var seats = request.Seats.Select(s => new Seat
            {
                VenueId = request.VenueId,
                Section = request.Screen,
                Row = s.Row,
                SeatNumber = s.SeatNumber,
                SeatType = s.SeatType.Trim()
            }).ToList();

            await _seatRepo.ReplaceScreenSeats(request.VenueId, request.Screen, seats);
            return _mapper.Map<List<SeatDto>>(seats);
        }

        /// <summary>
        /// A ticket type's quantity is the number of venue seats whose type matches by name, so
        /// "VIP" and "vip" would split one tier's seats across two types and undercount both.
        /// Seat types are therefore kept to one spelling per venue.
        /// </summary>
        private static void EnsureSeatTypesAreConsistent(List<ScreenSeatDto> incoming, List<Seat> retained)
        {
            foreach (var seat in incoming)
            {
                if (string.IsNullOrWhiteSpace(seat.SeatType))
                    throw new ValidationException("SeatType is required.");
                if (seat.SeatType.Trim().Length > MaxSeatTypeLength)
                    throw new ValidationException($"SeatType must not exceed {MaxSeatTypeLength} characters.");
            }

            var existingTypes = retained.Select(s => s.SeatType).Distinct().ToList();
            foreach (var type in incoming.Select(s => s.SeatType.Trim()).Distinct())
            {
                var clash = existingTypes.FirstOrDefault(e =>
                    string.Equals(e, type, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(e, type, StringComparison.Ordinal));

                if (clash is not null)
                    throw new ValidationException(
                        $"This venue already uses seat type '{clash}'. Use that spelling rather than '{type}'.");
            }
        }

        /// <summary>
        /// Seats are laid out one screen at a time, but capacity is declared for the venue as a
        /// whole — so a screen's seats have to fit alongside every other screen's. Pass the screen
        /// being replaced (its current seats are about to be discarded) or null when simply adding.
        /// </summary>
        private async Task EnsureWithinVenueCapacity(int venueId, string? replacingScreen, int incomingSeats)
        {
            var venue = await _venueRepo.GetById(venueId)
                ?? throw new NotFoundException($"Venue {venueId} not found.");

            var existing = await _seatRepo.GetByVenueId(venueId);
            var retained = replacingScreen is null
                ? existing.Count
                : existing.Count(s => s.Section != replacingScreen);

            var total = retained + incomingSeats;
            if (total > venue.TotalCapacity)
                throw new ValidationException(
                    $"Venue capacity is {venue.TotalCapacity} seats. This would bring the venue to {total} seats across all screens.");
        }

        public async Task DeleteScreen(int venueId, string screen)
        {
            if (venueId <= 0)
                throw new ValidationException("VenueId must be greater than zero.");
            if (string.IsNullOrWhiteSpace(screen))
                throw new ValidationException("Screen is required.");

            if (await _seatRepo.ScreenHasActiveSeatUsage(venueId, screen))
                throw new ValidationException("Cannot delete a screen that already has bookings.");

            await _seatRepo.ReplaceScreenSeats(venueId, screen, new List<Seat>());
        }
    }
}
