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
        private readonly IScreeningRepository _screeningRepo;
        private readonly IUserRepository _userRepo;
        private readonly ITicketTypeRepository _ticketTypeRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IMapper _mapper;
        private readonly IEmailQueue _emailQueue;

        // Events must be scheduled far enough ahead that ticket sales are viable and admins
        // have time to review before the event starts. Enforced on create and reschedule.
        private static readonly TimeSpan MinLeadTime = TimeSpan.FromHours(48);
        private const string LeadTimeMessage = "Events must be scheduled at least 2 days (48 hours) in advance.";

        public EventService(IEventRepository eventRepo, IVenueRepository venueRepo, IScreeningRepository screeningRepo,
            IUserRepository userRepo, ITicketTypeRepository ticketTypeRepo, IBookingRepository bookingRepo,
            ISeatRepository seatRepo, IMapper mapper, IEmailQueue emailQueue)
        {
            _eventRepo = eventRepo;
            _venueRepo = venueRepo;
            _screeningRepo = screeningRepo;
            _userRepo = userRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _bookingRepo = bookingRepo;
            _seatRepo = seatRepo;
            _mapper = mapper;
            _emailQueue = emailQueue;
        }

        public async Task<EventDto> Create(int organizerId, CreateEventRequest request)
        {
            InputValidator.ValidateRequiredString("Title", request.Title, 200);
            InputValidator.ValidateRequiredString("Description", request.Description, 2000);
            InputValidator.ValidateRequiredString("Category", request.Category, 100);
            InputValidator.ValidateUrl("ImageUrl", request.ImageUrl);

            var startUtc = TimeHelper.AssumeIstToUtc(request.StartTime);
            var endUtc = TimeHelper.AssumeIstToUtc(request.EndTime);

            if (startUtc < DateTime.UtcNow + MinLeadTime)
                throw new ValidationException(LeadTimeMessage);

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

            // Every event is bookable through at least one screening. Seed a default one
            // from the event's screen + time window; organizers add more via the screening API.
            await _screeningRepo.Add(new Screening
            {
                EventId = ev.Id,
                Screen = ev.Screen,
                StartTime = startUtc,
                EndTime = endUtc,
                Status = ScreeningStatus.Scheduled
            });

            return _mapper.Map<EventDto>(ev);
        }

        // Create an event that runs across several showtimes. Each showtime is its own screening
        // (a screen may repeat at different times), and the shared ticket categories are created
        // against every screening with each screening's quantity taken from that screen's seats.
        public async Task<EventDto> CreateWithScreenings(int organizerId, CreateEventWithScreeningsRequest request)
        {
            InputValidator.ValidateRequiredString("Title", request.Title, 200);
            InputValidator.ValidateRequiredString("Description", request.Description, 2000);
            InputValidator.ValidateRequiredString("Category", request.Category, 100);
            InputValidator.ValidateUrl("ImageUrl", request.ImageUrl);

            if (request.Showtimes == null || request.Showtimes.Count == 0)
                throw new ValidationException("At least one showtime is required.");
            if (request.TicketCategories == null || request.TicketCategories.Count == 0)
                throw new ValidationException("At least one ticket category is required.");

            _ = await _venueRepo.GetById(request.VenueId)
                ?? throw new NotFoundException($"Venue {request.VenueId} not found.");

            // Categories are shared across screens, so each seat type may back only one of them.
            foreach (var category in request.TicketCategories)
            {
                InputValidator.ValidateRequiredString("Ticket category name", category.Name, 100);
                if (string.IsNullOrWhiteSpace(category.SeatType))
                    throw new ValidationException("SeatType is required for every ticket category.");
                if (category.Price < 0)
                    throw new ValidationException("Price must be zero or greater.");
            }
            var seatTypes = request.TicketCategories.Select(c => c.SeatType.Trim()).ToList();
            if (seatTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != seatTypes.Count)
                throw new ValidationException("Each seat type can back only one ticket category.");

            // Validate every showtime and convert its IST wall-clock time to UTC for storage.
            var now = DateTime.UtcNow;
            var showtimes = new List<(string Screen, DateTime StartUtc, DateTime EndUtc)>();
            foreach (var showtime in request.Showtimes)
            {
                if (string.IsNullOrWhiteSpace(showtime.Screen))
                    throw new ValidationException("Every showtime must have a screen.");

                var startUtc = TimeHelper.AssumeIstToUtc(showtime.StartTime);
                var endUtc = TimeHelper.AssumeIstToUtc(showtime.EndTime);

                if (endUtc <= startUtc)
                    throw new ValidationException($"Showtime on screen '{showtime.Screen.Trim()}' must end after it starts.");
                if (startUtc < now + MinLeadTime)
                    throw new ValidationException(LeadTimeMessage);

                showtimes.Add((showtime.Screen.Trim(), startUtc, endUtc));
            }

            // The same screen cannot run two overlapping showtimes.
            foreach (var perScreen in showtimes.GroupBy(s => s.Screen))
            {
                var ordered = perScreen.OrderBy(s => s.StartUtc).ToList();
                for (int i = 1; i < ordered.Count; i++)
                    if (ordered[i].StartUtc < ordered[i - 1].EndUtc)
                        throw new ValidationException($"Screen '{perScreen.Key}' has overlapping showtimes.");
            }

            // A shared category must be sellable on every screen, so its seat type has to exist on
            // each. Resolve the per-screen capacity up front so nothing is created if any is missing.
            var distinctScreens = showtimes.Select(s => s.Screen).Distinct().ToList();
            var capacityByScreenAndType = new Dictionary<(string Screen, string SeatType), int>();
            foreach (var screen in distinctScreens)
            {
                foreach (var seatType in seatTypes)
                {
                    var count = await _seatRepo.CountByVenueSectionAndType(request.VenueId, screen, seatType);
                    if (count == 0)
                        throw new ValidationException($"No seats of type '{seatType}' exist on screen '{screen}'.");
                    capacityByScreenAndType[(screen, seatType)] = count;
                }
            }

            // All valid — create the event (window spans every showtime), the screenings, and the
            // shared ticket types on each screening.
            var ev = new Event
            {
                OrganizerId = organizerId,
                VenueId = request.VenueId,
                Title = request.Title,
                Description = request.Description,
                Status = EventStatus.Draft,
                StartTime = showtimes.Min(s => s.StartUtc),
                EndTime = showtimes.Max(s => s.EndUtc),
                ImageUrl = request.ImageUrl,
                Category = request.Category,
                Slug = await GenerateUniqueSlug(request.Title),
                Screen = string.Empty
            };
            await _eventRepo.Add(ev);

            foreach (var showtime in showtimes)
            {
                var screening = new Screening
                {
                    EventId = ev.Id,
                    Screen = showtime.Screen,
                    StartTime = showtime.StartUtc,
                    EndTime = showtime.EndUtc,
                    Status = ScreeningStatus.Scheduled
                };
                await _screeningRepo.Add(screening);

                foreach (var category in request.TicketCategories)
                {
                    var seatType = category.SeatType.Trim();
                    var capacity = capacityByScreenAndType[(showtime.Screen, seatType)];
                    await _ticketTypeRepo.Add(new TicketType
                    {
                        ScreeningId = screening.Id,
                        Name = category.Name.Trim(),
                        SeatType = seatType,
                        Price = category.Price,
                        TotalQuantity = capacity,
                        AvailableQuantity = capacity,
                        SaleStart = now,
                        SaleEnd = showtime.StartUtc,
                        IsActive = true
                    });
                }
            }

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
                TimeHelper.AssumeIstToUtc(request.StartFrom),
                TimeHelper.AssumeIstToUtc(request.StartTo),
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

            if (startUtc < DateTime.UtcNow + MinLeadTime)
                throw new ValidationException(LeadTimeMessage);

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

            // Keep the auto-created default screening in step with the event window. When the
            // organizer has added multiple screenings they manage those windows themselves, so
            // we only sync the single-screening case to avoid clobbering deliberate schedules.
            var screenings = await _screeningRepo.GetByEventId(id);
            if (screenings.Count == 1)
            {
                var screening = screenings[0];
                screening.StartTime = startUtc;
                screening.EndTime = endUtc;
                screening.Screen = ev.Screen;
                await _screeningRepo.Update(screening);
            }

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

            // An admin submitting publishes outright — there is nothing left to review.
            if (ev.Status == EventStatus.PendingApproval)
            {
                var admins = await _userRepo.GetAdmins();
                await _emailQueue.EnqueueMany(admins.Select(a => new QueuedEmail(
                    a.Email, a.Name, EmailTemplateKey.AdminReviewPending,
                    "An event is awaiting review",
                    new Dictionary<string, string>
                    {
                        ["Name"] = a.Name,
                        ["ItemType"] = "Event",
                        ["ItemTitle"] = ev.Title
                    },
                    DedupeKey: null,
                    SendAfter: null)).ToList());
            }

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

            // An event people have already booked into cannot simply be cancelled;
            // only cancelled bookings (which no longer hold seats) are ignored.
            var bookings = await _bookingRepo.GetByEventId(id);
            if (bookings.Any(b => b.BookingStatus != "Cancelled"))
                throw new ValidationException("Cannot cancel an event that has bookings.");

            ev.Status = EventStatus.Cancelled;
            ev.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.Update(ev);
            return _mapper.Map<EventDto>(ev);
        }

        // Admin-only operations
        public async Task<List<PendingEventReviewDto>> GetPendingApproval()
        {
            var events = await _eventRepo.GetByStatus(EventStatus.PendingApproval);
            var venues = (await _venueRepo.GetAll() ?? new List<Venue>()).ToDictionary(v => v.Id);

            var result = new List<PendingEventReviewDto>();
            foreach (var ev in events)
            {
                var dto = new PendingEventReviewDto
                {
                    Id = ev.Id,
                    Title = ev.Title,
                    Description = ev.Description,
                    Category = ev.Category,
                    ImageUrl = ev.ImageUrl,
                    StartTime = TimeHelper.UtcToIst(ev.StartTime),
                    EndTime = TimeHelper.UtcToIst(ev.EndTime),
                    Screen = ev.Screen,
                    CreatedAt = TimeHelper.UtcToIst(ev.CreatedAt),
                    RejectionReason = ev.RejectionReason,
                    VenueId = ev.VenueId,
                };

                if (venues.TryGetValue(ev.VenueId, out var venue))
                {
                    dto.VenueName = venue.Name;
                    dto.City = venue.City;
                }

                dto.Organizer = await BuildOrganizerSummary(ev.OrganizerId);
                dto.Screens = await BuildScreens(ev.Id);
                var allCategories = dto.Screens.SelectMany(s => s.TicketCategories).ToList();
                dto.Signals = BuildReviewSignals(ev, allCategories);

                result.Add(dto);
            }

            return result;
        }

        private async Task<OrganizerSummaryDto> BuildOrganizerSummary(int organizerId)
        {
            var user = await _userRepo.GetById(organizerId);
            var (published, rejected, total) = await _eventRepo.GetStatusCountsByOrganizer(organizerId);
            return new OrganizerSummaryDto
            {
                Id = organizerId,
                Name = user?.Name ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                Phone = user?.Phone ?? string.Empty,
                MemberSince = user != null ? TimeHelper.UtcToIst(user.CreatedAt) : default,
                IsActive = user?.IsActive ?? false,
                PublishedEventCount = published,
                RejectedEventCount = rejected,
                TotalEventCount = total,
            };
        }

        // Groups an event's screenings by screen: each screen carries its showtimes and its
        // own ticket categories. Per-screen capacity is consistent across a screen's showtimes,
        // so categories are taken from the screen's representative (first) screening.
        private async Task<List<ScreenReviewDto>> BuildScreens(int eventId)
        {
            var screenings = await _screeningRepo.GetByEventId(eventId);
            var screens = new List<ScreenReviewDto>();

            foreach (var group in screenings.GroupBy(s => s.Screen))
            {
                var ordered = group.OrderBy(s => s.StartTime).ToList();
                var representative = ordered.First();
                var ticketTypes = await _ticketTypeRepo.GetByScreeningId(representative.Id);

                screens.Add(new ScreenReviewDto
                {
                    Screen = group.Key,
                    Showtimes = ordered.Select(s => new ShowtimeDto
                    {
                        StartTime = TimeHelper.UtcToIst(s.StartTime),
                        EndTime = TimeHelper.UtcToIst(s.EndTime),
                    }).ToList(),
                    TicketCategories = ticketTypes.Select(t => new TicketCategorySummaryDto
                    {
                        Name = t.Name,
                        SeatType = t.SeatType,
                        Price = t.Price,
                        TotalQuantity = t.TotalQuantity,
                    }).ToList(),
                });
            }

            return screens;
        }

        // Format-only checks (no network calls) so the admin queue stays fast and deterministic.
        private static ReviewSignalsDto BuildReviewSignals(Event ev, List<TicketCategorySummaryDto> categories)
        {
            return new ReviewSignalsDto
            {
                LeadTimeOk = ev.StartTime >= ev.CreatedAt + MinLeadTime,
                ImageUrlValid = IsWellFormedHttpUrl(ev.ImageUrl),
                DescriptionAdequate = (ev.Description ?? string.Empty).Trim().Length >= 30,
                HasTicketCategories = categories.Count > 0,
                PricingSane = categories.Count > 0 && categories.All(c => c.Price > 0),
            };
        }

        private static bool IsWellFormedHttpUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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

            var organizer = await _userRepo.GetById(ev.OrganizerId);
            if (organizer != null)
            {
                await _emailQueue.Enqueue(organizer.Email, organizer.Name, EmailTemplateKey.EventApproved,
                    $"{ev.Title} is live",
                    new Dictionary<string, string>
                    {
                        ["Name"] = organizer.Name,
                        ["EventTitle"] = ev.Title
                    });
            }

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

            var organizer = await _userRepo.GetById(ev.OrganizerId);
            if (organizer != null)
            {
                await _emailQueue.Enqueue(organizer.Email, organizer.Name, EmailTemplateKey.EventRejected,
                    $"{ev.Title} wasn't approved",
                    new Dictionary<string, string>
                    {
                        ["Name"] = organizer.Name,
                        ["EventTitle"] = ev.Title,
                        ["Reason"] = string.IsNullOrWhiteSpace(reason) ? "No reason was provided." : reason
                    });
            }

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
