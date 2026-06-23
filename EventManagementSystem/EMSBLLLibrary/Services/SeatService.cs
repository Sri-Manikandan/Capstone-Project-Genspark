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
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;

        public SeatService(ISeatRepository seatRepo, IMapper mapper)
        {
            _seatRepo = seatRepo;
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

        public async Task<List<SeatDto>> GetAvailableByEventId(int eventId)
        {
            var seats = await _seatRepo.GetAvailableByEventId(eventId);
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

            var seats = request.Seats.Select(s => new Seat
            {
                VenueId = request.VenueId,
                Section = request.Screen,
                Row = s.Row,
                SeatNumber = s.SeatNumber,
                SeatType = s.SeatType
            }).ToList();

            await _seatRepo.ReplaceScreenSeats(request.VenueId, request.Screen, seats);
            return _mapper.Map<List<SeatDto>>(seats);
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
