using AutoMapper;
using EMSBLLLibrary.Helpers;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class TicketTypeService : ITicketTypeService
    {
        private readonly ITicketTypeRepository _ticketTypeRepo;
        private readonly IScreeningRepository _screeningRepo;
        private readonly IEventRepository _eventRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;

        public TicketTypeService(ITicketTypeRepository ticketTypeRepo, IScreeningRepository screeningRepo, IEventRepository eventRepo, ISeatRepository seatRepo, IMapper mapper)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _screeningRepo = screeningRepo;
            _eventRepo = eventRepo;
            _seatRepo = seatRepo;
            _mapper = mapper;
        }

        public async Task<TicketTypeDto> Create(int organizerId, CreateTicketTypeRequest request)
        {
            ValidateFields(request.Name, request.SeatType, request.Price);

            var screening = await _screeningRepo.GetById(request.ScreeningId)
                ?? throw new NotFoundException($"Screening {request.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to manage this event's tickets.");

            var (saleStartUtc, saleEndUtc) = ValidateSaleWindow(request.SaleStart, request.SaleEnd, screening.StartTime);

            var siblings = await _ticketTypeRepo.GetByScreeningId(request.ScreeningId);
            EnsureSeatTypeIsUnique(request.SeatType, siblings);

            // Quantity is not user input: it maps to the seats of this type on the screening's screen.
            var capacity = await SeatTypeCapacity(ev.VenueId, screening.Screen, request.SeatType);

            var ticketType = new TicketType
            {
                ScreeningId = request.ScreeningId,
                Name = request.Name,
                SeatType = request.SeatType,
                Price = request.Price,
                TotalQuantity = capacity,
                AvailableQuantity = capacity,
                SaleStart = saleStartUtc,
                SaleEnd = saleEndUtc,
                IsActive = true
            };
            await _ticketTypeRepo.Add(ticketType);
            return _mapper.Map<TicketTypeDto>(ticketType);
        }

        public async Task<TicketTypeDto> GetById(int id)
        {
            var tt = await _ticketTypeRepo.GetById(id)
                ?? throw new NotFoundException($"TicketType {id} not found.");
            return _mapper.Map<TicketTypeDto>(tt);
        }

        public async Task<List<TicketTypeDto>> GetByScreeningId(int screeningId)
        {
            var list = await _ticketTypeRepo.GetByScreeningId(screeningId);
            return _mapper.Map<List<TicketTypeDto>>(list);
        }

        public async Task<List<TicketTypeDto>> GetActiveByScreeningId(int screeningId)
        {
            var list = await _ticketTypeRepo.GetActiveByScreeningId(screeningId);
            return _mapper.Map<List<TicketTypeDto>>(list);
        }

        public async Task<TicketTypeDto> Update(int id, int organizerId, UpdateTicketTypeRequest request)
        {
            ValidateFields(request.Name, request.SeatType, request.Price);

            var tt = await _ticketTypeRepo.GetById(id)
                ?? throw new NotFoundException($"TicketType {id} not found.");

            var screening = await _screeningRepo.GetById(tt.ScreeningId)
                ?? throw new NotFoundException($"Screening {tt.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to update this ticket type.");

            var (saleStartUtc, saleEndUtc) = ValidateSaleWindow(request.SaleStart, request.SaleEnd, screening.StartTime);

            var soldQuantity = tt.TotalQuantity - tt.AvailableQuantity;
            var seatTypeChanged = request.SeatType != tt.SeatType;
            if (seatTypeChanged && soldQuantity > 0)
                throw new ValidationException("Cannot change seat type after tickets have sold.");

            if (seatTypeChanged)
            {
                var others = (await _ticketTypeRepo.GetByScreeningId(tt.ScreeningId)).Where(t => t.Id != id).ToList();
                EnsureSeatTypeIsUnique(request.SeatType, others);
            }

            // Quantity always tracks the screen's seats of this type (recomputed in case
            // it drifted); availability keeps whatever has already been sold.
            var capacity = await SeatTypeCapacity(ev.VenueId, screening.Screen, request.SeatType);

            tt.Name = request.Name;
            tt.SeatType = request.SeatType;
            tt.Price = request.Price;
            tt.TotalQuantity = capacity;
            tt.AvailableQuantity = capacity - soldQuantity;
            tt.SaleStart = saleStartUtc;
            tt.SaleEnd = saleEndUtc;
            tt.IsActive = request.IsActive;

            await _ticketTypeRepo.Update(tt);
            return _mapper.Map<TicketTypeDto>(tt);
        }

        public async Task Delete(int id, int organizerId)
        {
            var tt = await _ticketTypeRepo.GetById(id)
                ?? throw new NotFoundException($"TicketType {id} not found.");

            var screening = await _screeningRepo.GetById(tt.ScreeningId)
                ?? throw new NotFoundException($"Screening {tt.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to delete this ticket type.");

            await _ticketTypeRepo.Delete(id);
        }

        // Tickets must sell within a valid window that closes before the show starts.
        private static (DateTime SaleStartUtc, DateTime SaleEndUtc) ValidateSaleWindow(
            DateTime saleStart, DateTime saleEnd, DateTime screeningStartUtc)
        {
            var saleStartUtc = TimeHelper.AssumeIstToUtc(saleStart);
            var saleEndUtc = TimeHelper.AssumeIstToUtc(saleEnd);

            if (saleEndUtc <= saleStartUtc)
                throw new ValidationException("SaleEnd must be after SaleStart.");

            if (saleEndUtc > screeningStartUtc)
                throw new ValidationException("Ticket sales must end before the screening starts.");

            return (saleStartUtc, saleEndUtc);
        }

        private static void ValidateFields(string name, string seatType, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidationException("Ticket type name is required.");
            if (name.Trim().Length > 100)
                throw new ValidationException("Ticket type name must not exceed 100 characters.");
            if (string.IsNullOrWhiteSpace(seatType))
                throw new ValidationException("SeatType is required.");
            if (price < 0)
                throw new ValidationException("Price must be zero or greater.");
        }

        // Each seat type maps to exactly one ticket type per screening, because a ticket
        // type's quantity is the full seat-type capacity — sharing a type would oversell.
        private static void EnsureSeatTypeIsUnique(string seatType, IEnumerable<TicketType> siblings)
        {
            if (siblings.Any(t => t.SeatType == seatType))
                throw new ValidationException(
                    $"A ticket type for seat type '{seatType}' already exists for this screening.");
        }

        // A ticket type's quantity is the number of seats of that type on the screening's screen.
        // A screening with no specific screen (whole-venue event) counts every screen's seats.
        private async Task<int> SeatTypeCapacity(int venueId, string screen, string seatType)
        {
            var scopedToScreen = !string.IsNullOrWhiteSpace(screen);
            var seatCount = scopedToScreen
                ? await _seatRepo.CountByVenueSectionAndType(venueId, screen, seatType)
                : await _seatRepo.CountByVenueAndType(venueId, seatType);
            if (seatCount == 0)
                throw new ValidationException(scopedToScreen
                    ? $"No seats of type '{seatType}' exist on screen '{screen}'."
                    : $"No seats of type '{seatType}' exist in this venue.");
            return seatCount;
        }
    }
}
