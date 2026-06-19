using EMSBLLLibrary.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMSApplicationLayer.Controllers
{
    [ApiController]
    [Route("api/stripe")]
    [AllowAnonymous]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IStripeWebhookService _webhookService;
        
        public StripeWebhookController(IStripeWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var payload = await new StreamReader(Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();
            await _webhookService.ProcessAsync(payload, signature);
            return Ok();
        }
    }
}