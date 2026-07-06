using System.Security.Claims;
using GymMaster.API.DTOs;
using Microsoft.AspNetCore.Http;

namespace GymMaster.API.Services;

public interface IAccountService
{
    Task<AuthServiceResult<AuthUserResponse>> UpdateAsync(
        ClaimsPrincipal principal,
        UpdateMyAccountRequest request,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<PersonalProfileResponse>> GetProfileAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<PersonalProfileResponse>> UpdateProfileAsync(
        ClaimsPrincipal principal,
        UpdatePersonalProfileRequest request,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthUserResponse>> UploadAvatarAsync(
        ClaimsPrincipal principal,
        IFormFile? file,
        CancellationToken cancellationToken);
}
