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
    public class ScreeningController : ControllerBase
    {
        private readonly IScreeningService _screeningService;

        public ScreeningController(IScreeningService screeningService)
        {
            _screeningService = screeningService;
        }

        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var screenings = await _screeningService.GetByEventId(eventId);
            return Ok(screenings);
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var screening = await _screeningService.GetById(id);
            return Ok(screening);
        }

        [HttpPost]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateScreeningRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var screening = await _screeningService.Create(organizerId, request);
            return CreatedAtAction(nameof(GetById), new { id = screening.Id }, screening);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateScreeningRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var screening = await _screeningService.Update(id, organizerId, request);
            return Ok(screening);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            await _screeningService.Delete(id, organizerId);
            return NoContent();
        }
    }
}
