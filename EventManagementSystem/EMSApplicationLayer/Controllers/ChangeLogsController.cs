using Asp.Versioning;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMSApplicationLayer.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ChangeLogsController : ControllerBase
    {
        private readonly IChangeLogService _changeLogService;

        public ChangeLogsController(IChangeLogService changeLogService)
        {
            _changeLogService = changeLogService;
        }

        // GET /api/v1/changelogs?entityName=Event&userId=3&action=Update&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] ChangeLogQueryRequest request)
        {
            var result = await _changeLogService.Search(request);
            return Ok(result);
        }
    }
}
