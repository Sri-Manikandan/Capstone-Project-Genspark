using Asp.Versioning;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMSApplicationLayer.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/uploads")]
    public class UploadsController : ControllerBase
    {
        private readonly IImageStorage _storage;

        public UploadsController(IImageStorage storage)
        {
            _storage = storage;
        }

        // Organizers upload an event banner here, then send the returned URL as the event's
        // ImageUrl. Admins may upload too. Validation mirrors ImageContentType so the rule lives
        // in one place.
        [HttpPost("image")]
        [Authorize(Roles = "Organizer,Admin")]
        public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
        {
            var error = ImageContentType.Validate(file?.Length ?? 0, file?.ContentType);
            if (error is not null)
                throw new ValidationException(error);

            await using var stream = file!.OpenReadStream();
            var url = await _storage.SaveAsync(stream, file.ContentType, ct);
            return Ok(new { url });
        }
    }
}
