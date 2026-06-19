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
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("initiate")]
        [Idempotent]
        public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var payment = await _paymentService.Initiate(userId, request);
            return Ok(payment);
        }

        [HttpPost("confirm")]
        [Idempotent]
        public async Task<IActionResult> Confirm([FromBody] ConfirmPaymentRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var payment = await _paymentService.Confirm(userId, request);
            return Ok(payment);
        }

        [HttpGet("booking/{bookingId:int}")]
        public async Task<IActionResult> GetByBooking(int bookingId)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var payment = await _paymentService.GetByBookingId(bookingId, userId);
            return payment == null ? NotFound(new { error = "Payment not found." }) : Ok(payment);
        }
    }
}
