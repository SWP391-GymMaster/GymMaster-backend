using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using GymMaster.API.Common;
using GymMaster.API.Features.Auth;

namespace GymMaster.API.Features.Account;
public interface IAccountService
{
    Task<ServiceResult<AuthUserResponse>> UpdateAsync(
        ClaimsPrincipal principal,
        UpdateMyAccountRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<PersonalProfileResponse>> GetProfileAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<PersonalProfileResponse>> UpdateProfileAsync(
        ClaimsPrincipal principal,
        UpdatePersonalProfileRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<AuthUserResponse>> UploadAvatarAsync(
        ClaimsPrincipal principal,
        IFormFile? file,
        CancellationToken cancellationToken);
}
