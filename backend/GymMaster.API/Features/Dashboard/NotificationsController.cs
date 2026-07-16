using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymMaster.API.Common;

namespace GymMaster.API.Features.Dashboard;
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    // Notifications chưa có trong DB — trả về rỗng để frontend không bị 404.
    [HttpGet]
    public IActionResult GetNotifications([FromQuery] string? role)
    {
        return Ok(ApiResponse<object[]>.Ok([]));
    }

    [HttpPost("{id}/read")]
    public IActionResult MarkRead(string id)
    {
        return Ok(ApiResponse<object>.Ok(new { id, isRead = true }));
    }

    [HttpPost("read-all")]
    public IActionResult MarkAllRead([FromQuery] string? role)
    {
        return Ok(ApiResponse<object>.Ok(new { count = 0 }));
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        return Ok(ApiResponse<object>.Ok(new { success = true }));
    }
}
