using System.Text.RegularExpressions;
using AutoMapper;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Helpers;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;

namespace EMSBLLLibrary.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepo;
        private readonly IVenueRepository _venueRepo;
        private readonly IMapper _mapper;

        public EventService(IEventRepository eventRepo, IVenueRepository venueRepo, IMapper mapper)
        {
            _eventRepo = eventRepo;
            _venueRepo = venueRepo;
            _mapper = mapper;
        }

        public async Task<EventDto> Create(int organizerId, CreateEventRequest request)
        {
            InputValidator.ValidateRequiredString("Title", request.Title, 200);
            InputValidator.ValidateRequiredString("Description", request.Description, 2000);
            InputValidator.ValidateRequiredString("Category", request.Category, 100);
            InputValidator.ValidateUrl("ImageUrl", request.ImageUrl);

            var startUtc = TimeHelper.AssumeIstToUtc(request.StartTime);
            var endUtc = TimeHelper.AssumeIstToUtc(request.EndTime);

            if (startUtc <= DateTime.UtcNow)
                throw new ValidationException("StartTime must be in the future.");

            if (endUtc <= startUtc)
                throw new ValidationException("EndTime must be after StartTime.");

            _ = await _venueRepo.GetById(request.VenueId)
                ?? throw new NotFoundException($"Venue {request.VenueId} not found.");

            var ev = new Event
            {
                OrganizerId = organizerId,
                VenueId = request.VenueId,
                Title = request.Title,
                Description = request.Description,
                Status = EventStatus.Draft,
                StartTime = startUtc,
                EndTime = endUtc,
                ImageUrl = request.ImageUrl,
                Category = request.Category,
                Slug = await GenerateUniqueSlug(request.Title),
                Screen = request.Screen ?? string.Empty
            };
            await _eventRepo.Add(ev);
            return _mapper.Map<EventDto>(ev);
        }

        public async Task<EventDto> GetById(int id)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");
            return await AddVenue(_mapper.Map<EventDto>(ev));
        }

        public async Task<EventDto?> GetBySlug(string slug)
        {
            var ev = await _eventRepo.GetBySlug(slug);
            return ev == null ? null : await AddVenue(_mapper.Map<EventDto>(ev));
        }

        public async Task<List<EventDto>> GetAll()
        {
            var events = await _eventRepo.GetAll();
            return await AddVenues(_mapper.Map<List<EventDto>>(events));
        }

        public async Task<PagedResult<EventDto>> Search(EventSearchRequest request)
        {
            var (items, total) = await _eventRepo.Search(
                request.Query, request.Category, request.City, request.Status,
                request.StartFrom, request.StartTo,
                request.SortBy, request.SortOrder,
                request.Page, request.PageSize);

            return new PagedResult<EventDto>
            {
                Items = await AddVenues(_mapper.Map<List<EventDto>>(items)),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        public Task<List<string>> GetCategories()
        {
            return _eventRepo.GetCategories(EventStatus.Published);
        }

        public Task<List<string>> GetCities()
        {
            return _eventRepo.GetCities(EventStatus.Published);
        }

        public async Task<PagedResult<EventDto>> GetByOrganizer(int organizerId, int page, int pageSize)
        {
            var (items, total) = await _eventRepo.GetByOrganizerId(organizerId, page, pageSize);
            return new PagedResult<EventDto>
            {
                Items = await AddVenues(_mapper.Map<List<EventDto>>(items)),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<EventDto> Update(int id, int organizerId, UpdateEventRequest request)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");

            if (ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to update this event.");

            if (ev.Status == EventStatus.PendingApproval)
                throw new ValidationException("Cannot update an event that is pending admin approval. Cancel the submission first.");

            if (ev.Status == EventStatus.Published)
                throw new ValidationException("Cannot update a published event. Cancel it first.");

            InputValidator.ValidateRequiredString("Title", request.Title, 200);
            InputValidator.ValidateRequiredString("Description", request.Description, 2000);
            InputValidator.ValidateRequiredString("Category", request.Category, 100);
            InputValidator.ValidateUrl("ImageUrl", request.ImageUrl);

            var startUtc = TimeHelper.AssumeIstToUtc(request.StartTime);
            var endUtc = TimeHelper.AssumeIstToUtc(request.EndTime);

            if (endUtc <= startUtc)
                throw new ValidationException("EndTime must be after StartTime.");

            ev.Title = request.Title;
            ev.Description = request.Description;
            ev.StartTime = startUtc;
            ev.EndTime = endUtc;
            ev.ImageUrl = request.ImageUrl;
            ev.Category = request.Category;
            ev.Screen = request.Screen ?? string.Empty;
            ev.UpdatedAt = DateTime.UtcNow;

            await _eventRepo.Update(ev);
            return _mapper.Map<EventDto>(ev);
        }

        public async Task Delete(int id, int requesterId, bool isAdmin = false)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");

            if (!isAdmin && ev.OrganizerId != requesterId)
                throw new UnauthorizedException("Not authorized to delete this event.");

            await _eventRepo.Delete(id);
        }

        // Organizer submits a Draft/Rejected event for admin review.
        // Admin bypasses the approval queue and goes directly to Published.
        public async Task<EventDto> Submit(int id, int organizerId, bool isAdmin = false)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");

            if (!isAdmin && ev.OrganizerId != organizerId)
                throw new UnauthorizedException("Not authorized to submit this event.");

            if (ev.Status != EventStatus.Draft && ev.Status != EventStatus.Rejected)
                throw new ValidationException($"Only Draft or Rejected events can be submitted. Current status: {ev.Status}.");

            ev.Status = isAdmin ? EventStatus.Published : EventStatus.PendingApproval;
            ev.RejectionReason = null;
            ev.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.Update(ev);
            return _mapper.Map<EventDto>(ev);
        }

        public async Task<EventDto> Cancel(int id, int requesterId, bool isAdmin = false)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");

            if (!isAdmin && ev.OrganizerId != requesterId)
                throw new UnauthorizedException("Not authorized to cancel this event.");

            if (ev.Status == EventStatus.Cancelled)
                throw new ValidationException("Event is already cancelled.");

            ev.Status = EventStatus.Cancelled;
            ev.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.Update(ev);
            return _mapper.Map<EventDto>(ev);
        }

        // Admin-only operations
        public async Task<List<EventDto>> GetPendingApproval()
        {
            var events = await _eventRepo.GetByStatus(EventStatus.PendingApproval);
            return await AddVenues(_mapper.Map<List<EventDto>>(events));
        }

        public async Task<EventDto> AdminApprove(int id)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");

            if (ev.Status == EventStatus.Published)
                throw new ValidationException("Event is already published.");

            if (ev.Status == EventStatus.Cancelled)
                throw new ValidationException("Cannot approve a cancelled event.");

            ev.Status = EventStatus.Published;
            ev.RejectionReason = null;
            ev.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.Update(ev);
            return _mapper.Map<EventDto>(ev);
        }

        public async Task<EventDto> AdminReject(int id, string? reason)
        {
            var ev = await _eventRepo.GetById(id)
                ?? throw new NotFoundException($"Event {id} not found.");

            if (ev.Status != EventStatus.PendingApproval)
                throw new ValidationException($"Event is not pending approval. Current status: {ev.Status}.");

            ev.Status = EventStatus.Rejected;
            ev.RejectionReason = reason;
            ev.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.Update(ev);
            return _mapper.Map<EventDto>(ev);
        }

        // City and VenueName live on the venue; fill them in after mapping.
        private async Task<EventDto> AddVenue(EventDto dto)
        {
            var venue = await _venueRepo.GetById(dto.VenueId);
            if (venue != null)
            {
                dto.City = venue.City;
                dto.VenueName = venue.Name;
            }
            return dto;
        }

        private async Task<List<EventDto>> AddVenues(List<EventDto> dtos)
        {
            if (dtos.Count == 0) return dtos;
            var venues = (await _venueRepo.GetAll() ?? new List<Venue>()).ToDictionary(v => v.Id);
            foreach (var dto in dtos)
                if (venues.TryGetValue(dto.VenueId, out var venue))
                {
                    dto.City = venue.City;
                    dto.VenueName = venue.Name;
                }
            return dtos;
        }

        private async Task<string> GenerateUniqueSlug(string title)
        {
            var baseSlug = Regex.Replace(title.ToLower().Trim(), @"[^a-z0-9\s-]", "");
            baseSlug = Regex.Replace(baseSlug, @"\s+", "-").Trim('-');

            var slug = baseSlug;
            var counter = 1;
            while (await _eventRepo.GetBySlug(slug) != null)
                slug = $"{baseSlug}-{counter++}";

            return slug;
        }
    }
}
