using AutoMapper;
using EMSBLLLibrary.Helpers;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class VenueService : IVenueService
    {
        private readonly IVenueRepository _venueRepo;
        private readonly IEventRepository _eventRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;

        public VenueService(IVenueRepository venueRepo, IEventRepository eventRepo, ISeatRepository seatRepo, IMapper mapper)
        {
            _venueRepo = venueRepo;
            _eventRepo = eventRepo;
            _seatRepo = seatRepo;
            _mapper = mapper;
        }

        public async Task<VenueDto> Create(CreateVenueRequest request)
        {
            InputValidator.ValidateName(request.Name);
            InputValidator.ValidateRequiredString("Address", request.Address, 500);
            InputValidator.ValidateRequiredString("City", request.City, 100);
            InputValidator.ValidatePositiveInt("TotalCapacity", request.TotalCapacity);

            var venue = new Venue
            {
                Name = request.Name,
                Address = request.Address,
                City = request.City,
                TotalCapacity = request.TotalCapacity,
                LayoutConfig = request.LayoutConfig
            };
            await _venueRepo.Add(venue);
            return _mapper.Map<VenueDto>(venue);
        }

        public async Task<VenueDto> GetById(int id)
        {
            var venue = await _venueRepo.GetById(id)
                ?? throw new NotFoundException($"Venue {id} not found.");
            return _mapper.Map<VenueDto>(venue);
        }

        public async Task<List<VenueDto>> GetAll()
        {
            var venues = await _venueRepo.GetAll();
            return _mapper.Map<List<VenueDto>>(venues);
        }

        public async Task<VenueDto> Update(int id, UpdateVenueRequest request)
        {
            var venue = await _venueRepo.GetById(id)
                ?? throw new NotFoundException($"Venue {id} not found.");

            InputValidator.ValidateName(request.Name);
            InputValidator.ValidateRequiredString("Address", request.Address, 500);
            InputValidator.ValidateRequiredString("City", request.City, 100);
            InputValidator.ValidatePositiveInt("TotalCapacity", request.TotalCapacity);

            // Seats are validated against capacity when a screen is laid out; without this the
            // same limit could be broken from the other side by shrinking capacity afterwards.
            var seatCount = (await _seatRepo.GetByVenueId(id)).Count;
            if (request.TotalCapacity < seatCount)
                throw new ValidationException(
                    $"This venue already has {seatCount} seats across its screens. Capacity cannot be lower than that.");

            venue.Name = request.Name;
            venue.Address = request.Address;
            venue.City = request.City;
            venue.TotalCapacity = request.TotalCapacity;
            // Preserve the existing seat-map layout when the request omits it — the venue
            // form has no layout editor, so a blank value means "leave unchanged".
            if (!string.IsNullOrWhiteSpace(request.LayoutConfig))
                venue.LayoutConfig = request.LayoutConfig;

            await _venueRepo.Update(venue);
            return _mapper.Map<VenueDto>(venue);
        }

        public async Task Delete(int id)
        {
            var venue = await _venueRepo.GetById(id)
                ?? throw new NotFoundException($"Venue {id} not found.");

            if (await _eventRepo.ExistsByVenue(id))
                throw new ValidationException("Cannot delete a venue that has events. Delete or reassign its events first.");

            await _venueRepo.Delete(venue.Id);
        }
    }
}
