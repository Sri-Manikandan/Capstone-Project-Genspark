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
        private readonly IVenueRepository _venueRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;

        public TicketTypeService(ITicketTypeRepository ticketTypeRepo, IScreeningRepository screeningRepo, IEventRepository eventRepo, IVenueRepository venueRepo, ISeatRepository seatRepo, IMapper mapper)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _screeningRepo = screeningRepo;
            _eventRepo = eventRepo;
            _venueRepo = venueRepo;
            _seatRepo = seatRepo;
            _mapper = mapper;
        }

        public async Task<TicketTypeDto> Create(int organizerId, CreateTicketTypeRequest request)
        {
            ValidateFields(request.Name, request.SeatType, request.Price, request.TotalQuantity);

            var screening = await _screeningRepo.GetById(request.ScreeningId)
                ?? throw new NotFoundException($"Screening {request.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to manage this event's tickets.");

            var (saleStartUtc, saleEndUtc) = ValidateSaleWindow(request.SaleStart, request.SaleEnd, screening.StartTime);

            await ValidateAllocation(ev.VenueId, request.SeatType, request.TotalQuantity,
                await _ticketTypeRepo.GetByScreeningId(request.ScreeningId));

            var ticketType = new TicketType
            {
                ScreeningId = request.ScreeningId,
                Name = request.Name,
                SeatType = request.SeatType,
                Price = request.Price,
                TotalQuantity = request.TotalQuantity,
                AvailableQuantity = request.TotalQuantity,
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
            ValidateFields(request.Name, request.SeatType, request.Price, request.TotalQuantity);

            var tt = await _ticketTypeRepo.GetById(id)
                ?? throw new NotFoundException($"TicketType {id} not found.");

            var screening = await _screeningRepo.GetById(tt.ScreeningId)
                ?? throw new NotFoundException($"Screening {tt.ScreeningId} not found.");

            var ev = await _eventRepo.GetById(screening.EventId)
                ?? throw new NotFoundException($"Event {screening.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to update this ticket type.");

            var soldQuantity = tt.TotalQuantity - tt.AvailableQuantity;
            if (request.TotalQuantity < soldQuantity)
                throw new ValidationException($"Cannot reduce total quantity below {soldQuantity} (already sold).");

            var (saleStartUtc, saleEndUtc) = ValidateSaleWindow(request.SaleStart, request.SaleEnd, screening.StartTime);

            var others = (await _ticketTypeRepo.GetByScreeningId(tt.ScreeningId)).Where(t => t.Id != id).ToList();
            await ValidateAllocation(ev.VenueId, request.SeatType, request.TotalQuantity, others);

            tt.Name = request.Name;
            tt.SeatType = request.SeatType;
            tt.Price = request.Price;
            tt.AvailableQuantity = request.TotalQuantity - soldQuantity;
            tt.TotalQuantity = request.TotalQuantity;
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

        private static void ValidateFields(string name, string seatType, decimal price, int totalQuantity)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidationException("Ticket type name is required.");
            if (name.Trim().Length > 100)
                throw new ValidationException("Ticket type name must not exceed 100 characters.");
            if (string.IsNullOrWhiteSpace(seatType))
                throw new ValidationException("SeatType is required.");
            if (price < 0)
                throw new ValidationException("Price must be zero or greater.");
            if (totalQuantity <= 0)
                throw new ValidationException("TotalQuantity must be greater than zero.");
        }

        // Ticket allocation for a screening is bounded by the venue's seats of that type
        // (each screening tracks its own availability, so counts are per-screening siblings).
        private async Task ValidateAllocation(int venueId, string seatType, int totalQuantity, List<TicketType> siblings)
        {
            var venue = await _venueRepo.GetById(venueId)
                ?? throw new NotFoundException($"Venue {venueId} not found.");

            var seatCount = await _seatRepo.CountByVenueAndType(venueId, seatType);
            if (seatCount == 0)
                throw new ValidationException($"No seats of type '{seatType}' exist in this venue.");

            var allocatedForSameType = siblings.Where(t => t.SeatType == seatType).Sum(t => t.TotalQuantity);
            if (allocatedForSameType + totalQuantity > seatCount)
                throw new ValidationException(
                    $"Total quantity for '{seatType}' tickets ({allocatedForSameType + totalQuantity}) exceeds available '{seatType}' seats in the venue ({seatCount}).");

            var allocatedTotal = siblings.Sum(t => t.TotalQuantity);
            if (allocatedTotal + totalQuantity > venue.TotalCapacity)
                throw new ValidationException(
                    $"Total ticket quantity ({allocatedTotal + totalQuantity}) exceeds venue capacity ({venue.TotalCapacity}).");
        }
    }
}
