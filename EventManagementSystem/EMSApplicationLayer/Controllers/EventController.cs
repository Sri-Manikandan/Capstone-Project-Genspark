using Asp.Versioning;
using EMSApplicationLayer.Helpers;
using EMSBLLLibrary.Constants;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMSApplicationLayer.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class EventController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventController(IEventService eventService)
        {
            _eventService = eventService;
        }

        // Public browse — Admin sees all statuses; everyone else sees Published only
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] EventSearchRequest request)
        {
            if (!User.IsInRole("Admin"))
                request.Status = EventStatus.Published;

            var events = await _eventService.Search(request);
            return Ok(events);
        }

        // Public — distinct categories of Published events (for filter dropdown)
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _eventService.GetCategories();
            return Ok(categories);
        }

        // Public — distinct cities of Published events (for the location switcher)
        [HttpGet("cities")]
        public async Task<IActionResult> GetCities()
        {
            var cities = await _eventService.GetCities();
            return Ok(cities);
        }

        // Public detail — Admin sees all; Organizer sees own + Published; others Published only
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var ev = await _eventService.GetById(id);

            if (!User.IsInRole("Admin"))
            {
                var isOwnEvent = User.IsInRole("Organizer") &&
                                 ClaimsHelper.GetUserId(User) == ev.OrganizerId;

                if (!isOwnEvent && ev.Status != EventStatus.Published)
                    return NotFound(new { error = "Event not found." });
            }

            return Ok(ev);
        }

        // Public slug lookup — same visibility rules as GetById
        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var ev = await _eventService.GetBySlug(slug);
            if (ev == null) return NotFound(new { error = "Event not found." });

            if (!User.IsInRole("Admin"))
            {
                var isOwnEvent = User.IsInRole("Organizer") &&
                                 ClaimsHelper.GetUserId(User) == ev.OrganizerId;

                if (!isOwnEvent && ev.Status != EventStatus.Published)
                    return NotFound(new { error = "Event not found." });
            }

            return Ok(ev);
        }

        // Organizer/Admin — own events list (all statuses), paginated
        [HttpGet("my")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> GetMyEvents([FromQuery] MyEventsRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var events = await _eventService.GetByOrganizer(organizerId, request.Page, request.PageSize);
            return Ok(events);
        }

        // Organizer/Admin — create event (starts as Draft)
        [HttpPost]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var ev = await _eventService.Create(organizerId, request);
            return CreatedAtAction(nameof(GetById), new { id = ev.Id }, ev);
        }

        // Organizer/Admin — edit own event (only Draft or Rejected)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var ev = await _eventService.Update(id, organizerId, request);
            return Ok(ev);
        }

        // Organizer/Admin — delete own event; Admin can delete any
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var requesterId = ClaimsHelper.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            await _eventService.Delete(id, requesterId, isAdmin);
            return NoContent();
        }

        // Organizer — submit Draft/Rejected event for admin review
        // Admin — submit goes directly to Published (no self-approval required)
        [HttpPost("{id:int}/submit")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Submit(int id)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var ev = await _eventService.Submit(id, organizerId, isAdmin);
            return Ok(ev);
        }

        // Organizer — cancel own event; Admin — cancel any event
        [HttpPost("{id:int}/cancel")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Cancel(int id)
        {
            var requesterId = ClaimsHelper.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var ev = await _eventService.Cancel(id, requesterId, isAdmin);
            return Ok(ev);
        }
    }
}
