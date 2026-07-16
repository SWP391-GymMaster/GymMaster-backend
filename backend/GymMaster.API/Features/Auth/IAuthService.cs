using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Auth;
public interface IAuthService
{
    Task<ServiceResult<AuthLoginResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthLoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthLoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthLoginResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthUserResponse>> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<ServiceResult<object>> LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
