using AutoMapper;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class ScreeningService : IScreeningService
    {
        private readonly IScreeningRepository _screeningRepo;
        private readonly IEventRepository _eventRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;

        public ScreeningService(
            IScreeningRepository screeningRepo,
            IEventRepository eventRepo,
            ISeatRepository seatRepo,
            IMapper mapper)
        {
            _screeningRepo = screeningRepo;
            _eventRepo = eventRepo;
            _seatRepo = seatRepo;
            _mapper = mapper;
        }

        public async Task<List<ScreeningDto>> GetByEventId(int eventId)
        {
            var screenings = await _screeningRepo.GetByEventId(eventId);
            return _mapper.Map<List<ScreeningDto>>(screenings);
        }

        public async Task<ScreeningDto> GetById(int id)
        {
            var screening = await _screeningRepo.GetById(id)
                ?? throw new NotFoundException($"Screening {id} not found.");
            return _mapper.Map<ScreeningDto>(screening);
        }

        public async Task<ScreeningDto> Create(int organizerId, CreateScreeningRequest request)
        {
            var ev = await _eventRepo.GetById(request.EventId)
                ?? throw new NotFoundException($"Event {request.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to manage this event's screenings.");

            var (startUtc, endUtc) = await ValidateScreen(ev, request.Screen, request.StartTime, request.EndTime);

            var screening = new Screening
            {
                EventId = request.EventId,
                Screen = request.Screen.Trim(),
                StartTime = startUtc,
                EndTime = endUtc,
                Status = ScreeningStatus.Scheduled
            };
            await _screeningRepo.Add(screening);
            await RecomputeEventWindow(ev);
            return _mapper.Map<ScreeningDto>(screening);
        }

        public async Task<ScreeningDto> Update(int id, int organizerId, UpdateScreeningRequest request)
        {
            var screening = await _screeningRepo.GetById(id)
                ?? throw new NotFoundException($"Screening {id} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to update this screening.");

            if (await _screeningRepo.HasActivity(id))
                throw new ValidationException("Cannot edit a screening that already has bookings.");

            var (startUtc, endUtc) = await ValidateScreen(ev, request.Screen, request.StartTime, request.EndTime);

            screening.Screen = request.Screen.Trim();
            screening.StartTime = startUtc;
            screening.EndTime = endUtc;
            await _screeningRepo.Update(screening);
            await RecomputeEventWindow(ev);
            return _mapper.Map<ScreeningDto>(screening);
        }

        public async Task Delete(int id, int organizerId)
        {
            var screening = await _screeningRepo.GetById(id)
                ?? throw new NotFoundException($"Screening {id} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to delete this screening.");

            if (await _screeningRepo.HasActivity(id))
                throw new ValidationException("Cannot delete a screening that already has bookings.");

            await _screeningRepo.Delete(id);
            await RecomputeEventWindow(ev);
        }

        // The event window is the envelope of its screenings, so it moves whenever a screening
        // is added, retimed, or removed. With no screenings left there is nothing to derive from,
        // so the last known window is kept.
        private async Task RecomputeEventWindow(Event ev)
        {
            var screenings = await _screeningRepo.GetByEventId(ev.Id);
            if (screenings.Count == 0) return;

            ev.StartTime = screenings.Min(s => s.StartTime);
            ev.EndTime = screenings.Max(s => s.EndTime);
            ev.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.Update(ev);
        }

        // The screen is a label for the show (e.g. "Screen 1"); the venue must have seats.
        private async Task<(DateTime StartUtc, DateTime EndUtc)> ValidateScreen(
            Event ev, string screen, DateTime start, DateTime end)
        {
            if (string.IsNullOrWhiteSpace(screen))
                throw new ValidationException("Screen is required.");

            var venueSeats = await _seatRepo.GetByVenueId(ev.VenueId);
            if (venueSeats.Count == 0)
                throw new ValidationException("This venue has no seats configured.");

            var startUtc = TimeHelper.AssumeIstToUtc(start);
            var endUtc = TimeHelper.AssumeIstToUtc(end);
            if (endUtc <= startUtc)
                throw new ValidationException("EndTime must be after StartTime.");

            if (startUtc <= DateTime.UtcNow)
                throw new ValidationException("Screening start time must be in the future.");

            // The event window is derived from its screenings (see RecomputeEventWindow), so a
            // screening defines the window rather than being bounded by it — no envelope check here.
            return (startUtc, endUtc);
        }
    }
}
