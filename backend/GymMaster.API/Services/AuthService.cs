using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GymMaster.API.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TemporaryLockDuration = TimeSpan.FromMinutes(15);
    private static readonly string InvalidCredentialsMessage = "Email hoac mat khau khong dung.";

    private readonly GymMasterDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly GoogleAuthOptions _googleOptions;
    private readonly IWebHostEnvironment _environment;

    public AuthService(
        GymMasterDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IOptions<GoogleAuthOptions> googleOptions,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _googleOptions = googleOptions.Value;
        _environment = environment;
    }

    public async Task<AuthServiceResult<AuthLoginResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var phone = NormalizePhone(request.Phone);

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Failure<AuthLoginResponse>(
                "VALIDATION_ERROR",
                "Vui long nhap ho ten, email va mat khau.",
                StatusCodes.Status400BadRequest);
        }

        if (request.Password.Length < 6)
        {
            return Failure<AuthLoginResponse>(
                "VALIDATION_ERROR",
                "Mat khau phai co it nhat 6 ky tu.",
                StatusCodes.Status400BadRequest);
        }

        if (await _dbContext.Users.AnyAsync(user => user.Email == email && !user.IsDeleted, cancellationToken))
        {
            return Failure<AuthLoginResponse>(
                "EMAIL_EXISTS",
                "Email nay da duoc dang ky.",
                StatusCodes.Status409Conflict);
        }

        if (!string.IsNullOrWhiteSpace(phone) &&
            await _dbContext.Users.AnyAsync(user => user.Phone == phone && !user.IsDeleted, cancellationToken))
        {
            return Failure<AuthLoginResponse>(
                "PHONE_EXISTS",
                "So dien thoai nay da duoc dang ky.",
                StatusCodes.Status409Conflict);
        }

        var memberRole = await GetRoleAsync(RoleNames.Member, cancellationToken);
        var user = new User
        {
            Email = email,
            Phone = phone,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            Status = UserStatuses.Active
        };

        user.UserRoles.Add(new UserRole { User = user, Role = memberRole });
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await CreateLoginResponseAsync(user, cancellationToken, StatusCodes.Status201Created);
    }

    public async Task<AuthServiceResult<AuthLoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email ?? request.Identifier);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Failure<AuthLoginResponse>(
                "VALIDATION_ERROR",
                "Vui long nhap email va mat khau.",
                StatusCodes.Status400BadRequest);
        }

        var user = await FindUserWithRolesAsync(email, cancellationToken);

        if (user is null)
        {
            return Failure<AuthLoginResponse>(
                "INVALID_CREDENTIALS",
                InvalidCredentialsMessage,
                StatusCodes.Status401Unauthorized);
        }

        if (IsTemporarilyLocked(user))
        {
            return Failure<AuthLoginResponse>(
                "TOO_MANY_ATTEMPTS",
                "Tai khoan dang bi khoa tam thoi. Vui long thu lai sau 15 phut.",
                StatusCodes.Status429TooManyRequests);
        }

        if (user.Status == UserStatuses.Locked)
        {
            return Failure<AuthLoginResponse>(
                "ACCOUNT_LOCKED",
                "Tai khoan da bi khoa.",
                StatusCodes.Status423Locked);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await TrackFailedLoginAsync(user, cancellationToken);

            return user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow
                ? Failure<AuthLoginResponse>(
                    "TOO_MANY_ATTEMPTS",
                    "Dang nhap sai qua nhieu lan. Tai khoan bi khoa tam thoi 15 phut.",
                    StatusCodes.Status429TooManyRequests)
                : Failure<AuthLoginResponse>(
                    "INVALID_CREDENTIALS",
                    InvalidCredentialsMessage,
                    StatusCodes.Status401Unauthorized);
        }

        ResetLoginFailures(user);
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        return await CreateLoginResponseAsync(user, cancellationToken);
    }

    public async Task<AuthServiceResult<AuthLoginResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Failure<AuthLoginResponse>(
                "VALIDATION_ERROR",
                "Thieu refresh token.",
                StatusCodes.Status400BadRequest);
        }

        var activeTokens = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(token => token.RevokedAt == null && token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var matchedToken = activeTokens.FirstOrDefault(token =>
            BCrypt.Net.BCrypt.Verify(request.RefreshToken, token.TokenHash));

        if (matchedToken is null)
        {
            return Failure<AuthLoginResponse>(
                "INVALID_REFRESH_TOKEN",
                "Refresh token khong hop le hoac da het han.",
                StatusCodes.Status401Unauthorized);
        }

        matchedToken.RevokedAt = DateTime.UtcNow;
        return await CreateLoginResponseAsync(matchedToken.User, cancellationToken);
    }

    public async Task<AuthServiceResult<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return Failure<ForgotPasswordResponse>(
                "VALIDATION_ERROR",
                "Vui long nhap email.",
                StatusCodes.Status400BadRequest);
        }

        const string message = "Neu email ton tai, he thong se tao yeu cau dat lai mat khau.";
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            item => item.Email == email && !item.IsDeleted,
            cancellationToken);

        if (user is null)
        {
            return AuthServiceResult<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(message, null));
        }

        var resetToken = CreateShortToken();
        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken, 12),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(
            message,
            _environment.IsDevelopment() ? resetToken : null));
    }

    public async Task<AuthServiceResult<object>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResetToken) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Failure<object>(
                "VALIDATION_ERROR",
                "Vui long nhap reset token va mat khau moi.",
                StatusCodes.Status400BadRequest);
        }

        if (request.NewPassword.Length < 6)
        {
            return Failure<object>(
                "VALIDATION_ERROR",
                "Mat khau moi phai co it nhat 6 ky tu.",
                StatusCodes.Status400BadRequest);
        }

        var activeTokens = await _dbContext.PasswordResetTokens
            .Include(token => token.User)
            .Where(token => token.UsedAt == null &&
                token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var matchedToken = activeTokens.FirstOrDefault(token =>
            BCrypt.Net.BCrypt.Verify(request.ResetToken, token.TokenHash));

        if (matchedToken is null || matchedToken.User.IsDeleted)
        {
            return Failure<object>(
                "INVALID_RESET_TOKEN",
                "Reset token khong hop le hoac da het han.",
                StatusCodes.Status401Unauthorized);
        }

        var user = matchedToken.User;
        matchedToken.UsedAt = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        user.FailedLoginCount = 0;
        user.LoginWindowStartedAt = null;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await RevokeRefreshTokensAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<object>.Success(new { message = "Dat lai mat khau thanh cong." });
    }

    public async Task<AuthServiceResult<object>> ChangePasswordAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Failure<object>(
                "VALIDATION_ERROR",
                "Vui long nhap mat khau hien tai va mat khau moi.",
                StatusCodes.Status400BadRequest);
        }

        if (request.NewPassword.Length < 6)
        {
            return Failure<object>(
                "VALIDATION_ERROR",
                "Mat khau moi phai co it nhat 6 ky tu.",
                StatusCodes.Status400BadRequest);
        }

        var user = await GetUserFromPrincipalAsync(principal, cancellationToken);

        if (user is null)
        {
            return Failure<object>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Failure<object>(
                "INVALID_CURRENT_PASSWORD",
                "Mat khau hien tai khong dung.",
                StatusCodes.Status401Unauthorized);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        user.UpdatedAt = DateTime.UtcNow;
        await RevokeRefreshTokensAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<object>.Success(new { message = "Doi mat khau thanh cong." });
    }

    public async Task<AuthServiceResult<AuthLoginResponse>> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_googleOptions.ClientId))
        {
            return Failure<AuthLoginResponse>(
                "GOOGLE_NOT_CONFIGURED",
                "Google ClientId chua duoc cau hinh.",
                StatusCodes.Status500InternalServerError);
        }

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return Failure<AuthLoginResponse>(
                "VALIDATION_ERROR",
                "Thieu Google ID token.",
                StatusCodes.Status400BadRequest);
        }

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_googleOptions.ClientId]
                });
        }
        catch (InvalidJwtException)
        {
            return Failure<AuthLoginResponse>(
                "INVALID_GOOGLE_TOKEN",
                "Google ID token khong hop le.",
                StatusCodes.Status400BadRequest);
        }

        var email = NormalizeEmail(payload.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return Failure<AuthLoginResponse>(
                "VALIDATION_ERROR",
                "Google account khong co email hop le.",
                StatusCodes.Status400BadRequest);
        }

        var user = await FindUserWithRolesAsync(email, cancellationToken);

        if (user is null)
        {
            var memberRole = await GetRoleAsync(RoleNames.Member, cancellationToken);
            user = new User
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? email : payload.Name,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(CreateSecureToken(), 12),
                Status = UserStatuses.Active
            };

            user.UserRoles.Add(new UserRole { User = user, Role = memberRole });
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (user.Status == UserStatuses.Locked || IsTemporarilyLocked(user))
        {
            return Failure<AuthLoginResponse>(
                "ACCOUNT_LOCKED",
                "Tai khoan da bi khoa.",
                StatusCodes.Status423Locked);
        }

        return await CreateLoginResponseAsync(user, cancellationToken);
    }

    public async Task<AuthServiceResult<AuthUserResponse>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await GetUserFromPrincipalAsync(principal, cancellationToken);

        if (user is null)
        {
            return Failure<AuthUserResponse>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        if (user.Status == UserStatuses.Locked || IsTemporarilyLocked(user))
        {
            return Failure<AuthUserResponse>(
                "ACCOUNT_LOCKED",
                "Tai khoan da bi khoa.",
                StatusCodes.Status423Locked);
        }

        return AuthServiceResult<AuthUserResponse>.Success(ToUserResponse(user));
    }

    public async Task<AuthServiceResult<object>> LogoutAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);

        if (userId is null)
        {
            return Failure<object>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId &&
                token.RevokedAt == null &&
                token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AuthServiceResult<object>.Success(new { });
    }

    private async Task<AuthServiceResult<AuthLoginResponse>> CreateLoginResponseAsync(
        User user,
        CancellationToken cancellationToken,
        int statusCode = StatusCodes.Status200OK)
    {
        var role = GetPrimaryRole(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var accessToken = CreateAccessToken(user, role, expiresAt);
        var refreshToken = CreateSecureToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken, 12),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthServiceResult<AuthLoginResponse>.Success(new AuthLoginResponse(
            accessToken,
            refreshToken,
            expiresAt,
            ToUserResponse(user),
            role,
            GetRedirectPath(role)),
            statusCode);
    }

    private string CreateAccessToken(User user, string role, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
        {
            throw new InvalidOperationException("Jwt:SecretKey chua duoc cau hinh.");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _jwtOptions.Issuer,
            _jwtOptions.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task TrackFailedLoginAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (user.LoginWindowStartedAt is null ||
            now - user.LoginWindowStartedAt > FailedAttemptWindow)
        {
            user.LoginWindowStartedAt = now;
            user.FailedLoginCount = 1;
        }
        else
        {
            user.FailedLoginCount++;
        }

        if (user.FailedLoginCount > MaxFailedAttempts)
        {
            user.LockedUntil = now.Add(TemporaryLockDuration);
        }

        user.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ResetLoginFailures(User user)
    {
        user.FailedLoginCount = 0;
        user.LoginWindowStartedAt = null;
        user.LockedUntil = null;
    }

    private static bool IsTemporarilyLocked(User user)
    {
        return user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow;
    }

    private async Task<User?> GetUserFromPrincipalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);

        return userId is null
            ? null
            : await _dbContext.Users
                .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                .FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
    }

    private async Task<User?> FindUserWithRolesAsync(string email, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Email == email && !item.IsDeleted, cancellationToken);
    }

    private async Task<Role> GetRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);

        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            Name = roleName,
            Description = $"{roleName} role"
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    private async Task RevokeRefreshTokensAsync(long userId, CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId &&
                token.RevokedAt == null &&
                token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }
    }

    private static string GetPrimaryRole(User user)
    {
        return user.UserRoles.Select(userRole => userRole.Role.Name).FirstOrDefault() ?? RoleNames.Member;
    }

    private static string CreateSecureToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string CreateShortToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }

    private static long? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string? NormalizePhone(string? phone)
    {
        return string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    private static AuthUserResponse ToUserResponse(User user)
    {
        return new AuthUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            GetPrimaryRole(user),
            user.Status);
    }

    private static string GetRedirectPath(string role)
    {
        return role switch
        {
            RoleNames.Admin => "/admin",
            RoleNames.Staff => "/staff",
            RoleNames.Pt => "/pt",
            _ => "/member"
        };
    }

    private static AuthServiceResult<T> Failure<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
