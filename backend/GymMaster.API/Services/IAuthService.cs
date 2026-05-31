using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IAuthService
{
    Task<AuthServiceResult<AuthLoginResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthLoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthLoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<object>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthLoginResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken);

    Task<AuthServiceResult<AuthUserResponse>> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthServiceResult<object>> LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
