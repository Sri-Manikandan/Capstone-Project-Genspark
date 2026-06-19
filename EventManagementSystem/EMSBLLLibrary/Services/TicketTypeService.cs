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
        private readonly IEventRepository _eventRepo;
        private readonly IVenueRepository _venueRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;

        public TicketTypeService(ITicketTypeRepository ticketTypeRepo, IEventRepository eventRepo, IVenueRepository venueRepo, ISeatRepository seatRepo, IMapper mapper)
        {
            _ticketTypeRepo = ticketTypeRepo;
            _eventRepo = eventRepo;
            _venueRepo = venueRepo;
            _seatRepo = seatRepo;
            _mapper = mapper;
        }

        public async Task<TicketTypeDto> Create(int organizerId, CreateTicketTypeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ValidationException("Ticket type name is required.");
            if (request.Name.Trim().Length > 100)
                throw new ValidationException("Ticket type name must not exceed 100 characters.");
            if (string.IsNullOrWhiteSpace(request.SeatType))
                throw new ValidationException("SeatType is required.");
            if (request.Price < 0)
                throw new ValidationException("Price must be zero or greater.");
            if (request.TotalQuantity <= 0)
                throw new ValidationException("TotalQuantity must be greater than zero.");

            var ev = await _eventRepo.GetById(request.EventId)
                ?? throw new NotFoundException($"Event {request.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to manage this event's tickets.");

            var saleStartUtc = TimeHelper.AssumeIstToUtc(request.SaleStart);
            var saleEndUtc = TimeHelper.AssumeIstToUtc(request.SaleEnd);

            if (saleEndUtc <= saleStartUtc)
                throw new ValidationException("SaleEnd must be after SaleStart.");

            var venue = await _venueRepo.GetById(ev.VenueId)
                ?? throw new NotFoundException($"Venue {ev.VenueId} not found.");

            var seatCount = await _seatRepo.CountByVenueAndType(venue.Id, request.SeatType);
            if (seatCount == 0)
                throw new ValidationException($"No seats of type '{request.SeatType}' exist in this venue.");

            var existingTicketTypes = await _ticketTypeRepo.GetByEventId(request.EventId);
            var allocatedForSameType = existingTicketTypes
                .Where(t => t.SeatType == request.SeatType)
                .Sum(t => t.TotalQuantity);

            if (allocatedForSameType + request.TotalQuantity > seatCount)
                throw new ValidationException(
                    $"Total quantity for '{request.SeatType}' tickets ({allocatedForSameType + request.TotalQuantity}) exceeds available '{request.SeatType}' seats in the venue ({seatCount}).");

            var allocatedTotal = existingTicketTypes.Sum(t => t.TotalQuantity);
            if (allocatedTotal + request.TotalQuantity > venue.TotalCapacity)
                throw new ValidationException(
                    $"Total ticket quantity ({allocatedTotal + request.TotalQuantity}) exceeds venue capacity ({venue.TotalCapacity}).");

            var ticketType = new TicketType
            {
                EventId = request.EventId,
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

        public async Task<List<TicketTypeDto>> GetByEventId(int eventId)
        {
            var list = await _ticketTypeRepo.GetByEventId(eventId);
            return _mapper.Map<List<TicketTypeDto>>(list);
        }

        public async Task<List<TicketTypeDto>> GetActiveByEventId(int eventId)
        {
            var list = await _ticketTypeRepo.GetActiveByEventId(eventId);
            return _mapper.Map<List<TicketTypeDto>>(list);
        }

        public async Task<TicketTypeDto> Update(int id, int organizerId, UpdateTicketTypeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ValidationException("Ticket type name is required.");
            if (request.Name.Trim().Length > 100)
                throw new ValidationException("Ticket type name must not exceed 100 characters.");
            if (string.IsNullOrWhiteSpace(request.SeatType))
                throw new ValidationException("SeatType is required.");
            if (request.Price < 0)
                throw new ValidationException("Price must be zero or greater.");
            if (request.TotalQuantity <= 0)
                throw new ValidationException("TotalQuantity must be greater than zero.");

            var tt = await _ticketTypeRepo.GetById(id)
                ?? throw new NotFoundException($"TicketType {id} not found.");

            var ev = await _eventRepo.GetById(tt.EventId)
                ?? throw new NotFoundException($"Event {tt.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to update this ticket type.");

            var soldQuantity = tt.TotalQuantity - tt.AvailableQuantity;
            if (request.TotalQuantity < soldQuantity)
                throw new ValidationException($"Cannot reduce total quantity below {soldQuantity} (already sold).");

            var venue = await _venueRepo.GetById(ev.VenueId)
                ?? throw new NotFoundException($"Venue {ev.VenueId} not found.");

            var seatCount = await _seatRepo.CountByVenueAndType(venue.Id, request.SeatType);
            if (seatCount == 0)
                throw new ValidationException($"No seats of type '{request.SeatType}' exist in this venue.");

            var existingTicketTypes = await _ticketTypeRepo.GetByEventId(tt.EventId);
            var allocatedForSameType = existingTicketTypes
                .Where(t => t.Id != id && t.SeatType == request.SeatType)
                .Sum(t => t.TotalQuantity);

            if (allocatedForSameType + request.TotalQuantity > seatCount)
                throw new ValidationException(
                    $"Total quantity for '{request.SeatType}' tickets ({allocatedForSameType + request.TotalQuantity}) exceeds available '{request.SeatType}' seats in the venue ({seatCount}).");

            var allocatedTotal = existingTicketTypes.Where(t => t.Id != id).Sum(t => t.TotalQuantity);
            if (allocatedTotal + request.TotalQuantity > venue.TotalCapacity)
                throw new ValidationException(
                    $"Total ticket quantity ({allocatedTotal + request.TotalQuantity}) exceeds venue capacity ({venue.TotalCapacity}).");

            tt.Name = request.Name;
            tt.SeatType = request.SeatType;
            tt.Price = request.Price;
            tt.AvailableQuantity = request.TotalQuantity - soldQuantity;
            tt.TotalQuantity = request.TotalQuantity;
            tt.SaleStart = TimeHelper.AssumeIstToUtc(request.SaleStart);
            tt.SaleEnd = TimeHelper.AssumeIstToUtc(request.SaleEnd);
            tt.IsActive = request.IsActive;

            await _ticketTypeRepo.Update(tt);
            return _mapper.Map<TicketTypeDto>(tt);
        }

        public async Task Delete(int id, int organizerId)
        {
            var tt = await _ticketTypeRepo.GetById(id)
                ?? throw new NotFoundException($"TicketType {id} not found.");

            var ev = await _eventRepo.GetById(tt.EventId)
                ?? throw new NotFoundException($"Event {tt.EventId} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to delete this ticket type.");

            await _ticketTypeRepo.Delete(id);
        }
    }
}
