using GymMaster.API.DTOs;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    // Chuan hoa moi response ve wrapper { success, data, error, meta } (ARCH-02).
    protected IActionResult ToActionResult<T>(AuthServiceResult<T> result)
    {
        if (result.Succeeded)
        {
            return StatusCode(result.StatusCode, ApiResponse<T>.Ok(result.Value!));
        }

        var response = ApiResponse<T>.Fail(
            result.ErrorCode ?? "ERROR",
            result.ErrorMessage ?? "Yeu cau khong hop le.",
            HttpContext.TraceIdentifier);

        return StatusCode(result.StatusCode, response);
    }
}
