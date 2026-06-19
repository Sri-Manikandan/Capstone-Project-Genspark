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
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = ClaimsHelper.GetUserId(User);
            var user = await _userService.GetById(userId);
            return Ok(user);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            var user = await _userService.Update(userId, request);
            return Ok(user);
        }

        [HttpPut("me/password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            await _userService.ChangePassword(userId, request);
            return NoContent();
        }

        [HttpPut("me/email")]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            await _userService.ChangeEmail(userId, request);
            return NoContent();
        }

        [HttpDelete("me")]
        public async Task<IActionResult> CloseAccount([FromBody] CloseAccountRequest request)
        {
            var userId = ClaimsHelper.GetUserId(User);
            await _userService.DeactivateSelf(userId, request.Password);
            return NoContent();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] UserSearchRequest request)
        {
            var users = await _userService.GetAll(request);
            return Ok(users);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userService.GetById(id);
            return Ok(user);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _userService.Deactivate(id);
            return NoContent();
        }

        // Request to become an organizer (User role only)
        [HttpPost("request-organizer")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> RequestOrganizerRole()
        {
            var userId = ClaimsHelper.GetUserId(User);
            var result = await _userService.RequestOrganizerRole(userId);
            return Ok(result);
        }

        // Check the status of the current user's organizer request
        [HttpGet("organizer-request")]
        public async Task<IActionResult> GetMyOrganizerRequest()
        {
            var userId = ClaimsHelper.GetUserId(User);
            var result = await _userService.GetMyOrganizerRequest(userId);
            return result == null
                ? NotFound(new { error = "No organizer request found." })
                : Ok(result);
        }
    }
}
