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
    private const int ResetResendCooldownSeconds = 60;   // chong spam: 60s moi duoc xin OTP moi
    private const int MaxResetAttempts = 3;              // nhap sai OTP qua 3 lan -> vo hieu ma
    private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TemporaryLockDuration = TimeSpan.FromMinutes(15);
    private static readonly string InvalidCredentialsMessage = "Email hoac mat khau khong dung.";

    private readonly GymMasterDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly GoogleAuthOptions _googleOptions;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;

    public AuthService(
        GymMasterDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IOptions<GoogleAuthOptions> googleOptions,
        IGoogleTokenValidator googleTokenValidator,
        IWebHostEnvironment environment,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _googleOptions = googleOptions.Value;
        _googleTokenValidator = googleTokenValidator;
        _environment = environment;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
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

        var now = DateTime.UtcNow;

        // Chong spam: neu vua gui OTP trong vong 60s thi khong gui lai (van tra message chung).
        var latestToken = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestToken is not null &&
            latestToken.CreatedAt > now.AddSeconds(-ResetResendCooldownSeconds))
        {
            return AuthServiceResult<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(message, null));
        }

        // Vo hieu het OTP cu chua dung -> chi giu OTP moi nhat.
        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var old in activeTokens)
        {
            old.UsedAt = now;
        }

        var otp = CreateOtp();
        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(otp, 12),
            ExpiresAt = now.AddMinutes(30),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Da cau hinh SMTP -> gui email (OTP + link). Khong tra OTP ve response (bao mat).
        if (_emailOptions.IsConfigured)
        {
            await SendResetEmailAsync(user, otp, cancellationToken);
            return AuthServiceResult<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(message, null));
        }

        // Chua cau hinh email: fallback dev -> tra OTP ve de test (chi o moi truong Development).
        return AuthServiceResult<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(
            message,
            _environment.IsDevelopment() ? otp : null));
    }

    private async Task SendResetEmailAsync(User user, string otp, CancellationToken cancellationToken)
    {
        // Link nhay toi trang reset, dien san email; OTP nguoi dung tu nhap (gioi han 3 lan).
        var link = $"{_emailOptions.FrontendBaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(user.Email)}";
        var subject = "GymMaster - Ma dat lai mat khau (OTP)";
        var htmlBody = $"""
            <div style="font-family:Arial,sans-serif;font-size:14px;color:#111;line-height:1.6">
              <h2 style="margin:0 0 12px">Dat lai mat khau GymMaster</h2>
              <p>Xin chao {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
              <p>Ma OTP dat lai mat khau cua ban (hieu luc 30 phut, chi nhap toi da 3 lan):</p>
              <p style="font-family:monospace;font-size:32px;font-weight:bold;letter-spacing:6px;background:#f3f4f6;padding:14px 24px;border-radius:10px;display:inline-block;color:#16a34a">{otp}</p>
              <p style="margin:20px 0">
                <a href="{link}" style="background:#16a34a;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:bold">Mo trang dat lai mat khau</a>
              </p>
              <p style="color:#6b7280;margin-top:20px">Neu ban khong yeu cau, hay bo qua email nay.</p>
            </div>
            """;

        await _emailSender.SendAsync(user.Email, subject, htmlBody, cancellationToken);
    }

    public async Task<AuthServiceResult<object>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.ResetToken) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Failure<object>(
                "VALIDATION_ERROR",
                "Vui long nhap email, ma OTP va mat khau moi.",
                StatusCodes.Status400BadRequest);
        }

        if (request.NewPassword.Length < 6)
        {
            return Failure<object>(
                "VALIDATION_ERROR",
                "Mat khau moi phai co it nhat 6 ky tu.",
                StatusCodes.Status400BadRequest);
        }

        var email = NormalizeEmail(request.Email);
        var now = DateTime.UtcNow;

        // OTP 6 so khong duy nhat -> phai tim theo email + lay ma moi nhat con hieu luc.
        var matchedToken = await _dbContext.PasswordResetTokens
            .Include(token => token.User)
            .Where(token => token.User.Email == email &&
                token.UsedAt == null &&
                token.ExpiresAt > now)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (matchedToken is null || matchedToken.User.IsDeleted)
        {
            return Failure<object>(
                "INVALID_RESET_TOKEN",
                "Ma khong hop le hoac da het han. Vui long yeu cau ma moi.",
                StatusCodes.Status401Unauthorized);
        }

        // Sai OTP -> dem so lan; qua 3 lan thi vo hieu ma, bat xin ma moi.
        if (!BCrypt.Net.BCrypt.Verify(request.ResetToken, matchedToken.TokenHash))
        {
            matchedToken.AttemptCount++;

            if (matchedToken.AttemptCount >= MaxResetAttempts)
            {
                matchedToken.UsedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Failure<object>(
                    "TOO_MANY_ATTEMPTS",
                    "Ban da nhap sai qua 3 lan. Ma da bi vo hieu, vui long yeu cau ma moi.",
                    StatusCodes.Status401Unauthorized);
            }

            var remaining = MaxResetAttempts - matchedToken.AttemptCount;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Failure<object>(
                "INVALID_RESET_TOKEN",
                $"Ma OTP khong dung. Con {remaining} lan thu.",
                StatusCodes.Status401Unauthorized);
        }

        var user = matchedToken.User;
        matchedToken.UsedAt = now;
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

        GoogleTokenPayload payload;

        try
        {
            payload = await _googleTokenValidator.ValidateAsync(
                request.IdToken,
                _googleOptions.ClientId,
                cancellationToken);
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

        // Chi nhan email da duoc Google xac minh (chong dung email gia/chua verify).
        if (payload.EmailVerified != true)
        {
            return Failure<AuthLoginResponse>(
                "GOOGLE_EMAIL_NOT_VERIFIED",
                "Email Google chua duoc xac minh.",
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
                AvatarUrl = string.IsNullOrWhiteSpace(payload.Picture) ? null : payload.Picture,
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

        var memberProfileId = await GetMemberProfileIdAsync(user.Id, cancellationToken);
        return AuthServiceResult<AuthUserResponse>.Success(ToUserResponse(user, memberProfileId));
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

        var memberProfileId = await GetMemberProfileIdAsync(user.Id, cancellationToken);

        return AuthServiceResult<AuthLoginResponse>.Success(new AuthLoginResponse(
            accessToken,
            refreshToken,
            expiresAt,
            ToUserResponse(user, memberProfileId),
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

    // OTP 6 chu so (000000-999999), dung cho dat lai mat khau.
    private static string CreateOtp()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
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

    private static AuthUserResponse ToUserResponse(User user, long? memberProfileId = null)
    {
        return new AuthUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            GetPrimaryRole(user),
            user.Status,
            memberProfileId);
    }

    // Id ho so hoi vien (member_profiles) cua user, null neu khong phai member / chua co ho so.
    private async Task<long?> GetMemberProfileIdAsync(long userId, CancellationToken cancellationToken)
    {
        return await _dbContext.MemberProfiles
            .Where(profile => profile.UserId == userId && !profile.IsDeleted)
            .Select(profile => (long?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);
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
