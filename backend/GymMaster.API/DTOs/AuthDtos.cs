namespace GymMaster.API.DTOs;

public sealed class LoginRequest
{
    public string? Identifier { get; set; }

    public string? Email
    {
        get => Identifier;
        set => Identifier = value;
    }

    public string? Username
    {
        get => Identifier;
        set => Identifier = value;
    }

    public string? Password { get; set; }
}

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string? Phone,
    string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(
    string Message,
    string? ResetToken);

public sealed record ResetPasswordRequest(
    string? Email,
    string ResetToken,
    string NewPassword);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record GoogleLoginRequest(string IdToken);

public sealed record AuthLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    AuthUserResponse User,
    string Role,
    string RedirectPath);

public sealed record AuthUserResponse(
    long UserId,
    string Email,
    string FullName,
    string Role,
    string Status);

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    object? Meta)
{
    public static ApiResponse<T> Ok(T data, object? meta = null)
    {
        return new ApiResponse<T>(true, data, null, meta);
    }

    public static ApiResponse<T> Fail(string code, string message, string requestId)
    {
        return new ApiResponse<T>(false, default, new ApiError(code, message, requestId), null);
    }
}

public sealed record ApiError(string Code, string Message, string RequestId);
