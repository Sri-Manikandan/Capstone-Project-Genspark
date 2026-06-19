using Asp.Versioning;
using EMSApplicationLayer.Helpers;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMSApplicationLayer.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IEventService _eventService;

        public AdminController(IUserService userService, IEventService eventService)
        {
            _userService = userService;
            _eventService = eventService;
        }

        // ── Organizer upgrade requests ────────────────────────────────────────────

        // GET /api/admin/organizer-requests?status=Pending&page=1&pageSize=20
        [HttpGet("organizer-requests")]
        public async Task<IActionResult> GetOrganizerRequests([FromQuery] OrganizerRequestQueryRequest request)
        {
            var requests = await _userService.GetOrganizerRequests(request);
            return Ok(requests);
        }

        // POST /api/admin/organizer-requests/{id}/approve
        [HttpPost("organizer-requests/{id:int}/approve")]
        public async Task<IActionResult> ApproveOrganizerRequest(int id)
        {
            var adminId = ClaimsHelper.GetUserId(User);
            var result = await _userService.ApproveOrganizerRequest(id, adminId);
            return Ok(result);
        }

        // POST /api/admin/organizer-requests/{id}/reject
        [HttpPost("organizer-requests/{id:int}/reject")]
        public async Task<IActionResult> RejectOrganizerRequest(int id, [FromBody] ReviewOrganizerRequestRequest request)
        {
            var adminId = ClaimsHelper.GetUserId(User);
            var result = await _userService.RejectOrganizerRequest(id, adminId, request.Reason);
            return Ok(result);
        }

        // ── Event approval ────────────────────────────────────────────────────────

        // GET /api/admin/events/pending
        [HttpGet("events/pending")]
        public async Task<IActionResult> GetPendingEvents()
        {
            var events = await _eventService.GetPendingApproval();
            return Ok(events);
        }

        // POST /api/admin/events/{id}/approve
        [HttpPost("events/{id:int}/approve")]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var ev = await _eventService.AdminApprove(id);
            return Ok(ev);
        }

        // POST /api/admin/events/{id}/reject
        [HttpPost("events/{id:int}/reject")]
        public async Task<IActionResult> RejectEvent(int id, [FromBody] ReviewEventRequest request)
        {
            var ev = await _eventService.AdminReject(id, request.Reason);
            return Ok(ev);
        }

        // ── User management ───────────────────────────────────────────────────────

        // GET /api/admin/users?query=john&role=Organizer&isActive=true&page=1&pageSize=20
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] UserSearchRequest request)
        {
            var users = await _userService.GetAll(request);
            return Ok(users);
        }

        // DELETE /api/admin/users/{id}
        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            await _userService.Deactivate(id);
            return NoContent();
        }
    }
}
