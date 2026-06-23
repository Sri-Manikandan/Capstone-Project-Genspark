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
    public class SeatController : ControllerBase
    {
        private readonly ISeatService _seatService;
        private readonly ISeatReservationService _reservationService;

        public SeatController(ISeatService seatService, ISeatReservationService reservationService)
        {
            _seatService = seatService;
            _reservationService = reservationService;
        }

        [HttpGet("venue/{venueId:int}")]
        public async Task<IActionResult> GetByVenue(int venueId)
        {
            var seats = await _seatService.GetByVenueId(venueId);
            return Ok(seats);
        }

        [HttpGet("available/event/{eventId:int}")]
        public async Task<IActionResult> GetAvailable(int eventId)
        {
            var seats = await _seatService.GetAvailableByEventId(eventId);
            return Ok(seats);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateSeatRequest request)
        {
            var seat = await _seatService.Create(request);
            return Ok(seat);
        }

        [HttpPost("bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkCreate([FromBody] BulkCreateSeatsRequest request)
        {
            var seats = await _seatService.BulkCreate(request);
            return Ok(seats);
        }

        [HttpPut("screen")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetScreen([FromBody] SetScreenSeatsRequest request)
        {
            var seats = await _seatService.SetScreenSeats(request);
            return Ok(seats);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _seatService.Delete(id);
            return NoContent();
        }

        [HttpPost("reserve")]
        [Authorize]
        public async Task<IActionResult> Reserve([FromBody] ReserveSeatRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var reservation = await _reservationService.Reserve(userId, request);
            return Ok(reservation);
        }

        [HttpPost("reserve/{reservationId:int}/release")]
        [Authorize]
        public async Task<IActionResult> Release(int reservationId)
        {
            var userId = ClaimsHelper.GetUserId(User);
            await _reservationService.Release(reservationId, userId);
            return NoContent();
        }
    }
}
