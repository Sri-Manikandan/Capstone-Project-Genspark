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
    public class TicketTypeController : ControllerBase
    {
        private readonly ITicketTypeService _ticketTypeService;

        public TicketTypeController(ITicketTypeService ticketTypeService)
        {
            _ticketTypeService = ticketTypeService;
        }

        [HttpGet("event/{eventId:int}")]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var types = await _ticketTypeService.GetByEventId(eventId);
            return Ok(types);
        }

        [HttpGet("event/{eventId:int}/active")]
        public async Task<IActionResult> GetActiveByEvent(int eventId)
        {
            var types = await _ticketTypeService.GetActiveByEventId(eventId);
            return Ok(types);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tt = await _ticketTypeService.GetById(id);
            return Ok(tt);
        }

        [HttpPost]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateTicketTypeRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var tt = await _ticketTypeService.Create(organizerId, request);
            return Ok(tt);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTicketTypeRequest request)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            var tt = await _ticketTypeService.Update(id, organizerId, request);
            return Ok(tt);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var organizerId = ClaimsHelper.GetUserId(User);
            await _ticketTypeService.Delete(id, organizerId);
            return NoContent();
        }
    }
}
