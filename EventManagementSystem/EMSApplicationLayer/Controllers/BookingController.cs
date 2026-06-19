using Asp.Versioning;
using EMSApplicationLayer.Filters;
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
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost]
        [Idempotent]
        public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var booking = await _bookingService.Create(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var booking = await _bookingService.GetById(id, userId);
            return Ok(booking);
        }

        [HttpGet("reference/{reference}")]
        public async Task<IActionResult> GetByReference(string reference)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var booking = await _bookingService.GetByReference(reference, userId);
            return booking == null ? NotFound(new { error = "Booking not found." }) : Ok(booking);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyBookings([FromQuery] BookingQueryRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var bookings = await _bookingService.GetByUserId(userId, request);
            return Ok(bookings);
        }

        [HttpGet("event/{eventId:int}")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> GetByEvent(int eventId, [FromQuery] BookingQueryRequest request)
        {
            var bookings = await _bookingService.GetByEventId(eventId, request);
            return Ok(bookings);
        }

        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = ClaimsHelper.GetUserId(User);
            await _bookingService.Cancel(id, userId);
            return Ok(new { message = "Booking cancelled successfully." });
        }

        [HttpPost("validate-qr")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> ValidateQr([FromBody] ValidateQrRequest request)
        {
            var result = await _bookingService.ValidateQr(request);
            return result
                ? Ok(new { message = "Ticket validated successfully." })
                : BadRequest(new { error = "Invalid or already used ticket." });
        }
    }
}
