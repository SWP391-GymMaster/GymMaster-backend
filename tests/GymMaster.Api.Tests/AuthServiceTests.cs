using System.Security.Claims;
using GymMaster.API.Common;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Features.Auth;
using GymMaster.API.Infrastructure;
using GymMaster.API.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace GymMaster.Api.Tests;

// Spec 001 — Auth & RBAC. Truoc day chi duoc phu bang black-box qua HTTP.
// Test o day danh vao cac nhanh bien cua co che chong brute-force + vong doi token,
// la cho de sai ma khong ai thay (AC-03, AC-04, AC-05, AC-09a).
public class AuthServiceTests
{
    private const string Password = "matkhau123";

    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"auth-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    private static AuthService NewService(GymMasterDbContext db, RecordingEmailSender? mail = null)
    {
        var jwt = Options.Create(new JwtOptions
        {
            SecretKey = "khoa-bi-mat-du-dai-cho-hs256-toi-thieu-32-ky-tu",
            Issuer = "GymMaster",
            Audience = "GymMaster.Client",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

        return new AuthService(
            db,
            jwt,
            Options.Create(new GoogleAuthOptions()),
            new DevEnvironment(),
            mail ?? new RecordingEmailSender(),
            Options.Create(new EmailOptions()));
    }

    private static async Task<User> SeedUserAsync(
        GymMasterDbContext db,
        string email = "a@gym.test",
        string status = UserStatuses.Active)
    {
        var role = new Role { Id = 4, Name = RoleNames.Member };
        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 12),
            FullName = "Nguoi Dung",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { Role = role });

        db.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static ClaimsPrincipal PrincipalFor(User user)
        => new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) }, "test"));

    // ---------- AC-01/AC-02: dang nhap + thong bao loi CHUNG ----------

    [Fact]
    public async Task Login_with_correct_password_issues_access_and_refresh_token()
    {
        using var db = NewDb();
        await SeedUserAsync(db);

        var result = await NewService(db).LoginAsync(
            new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.Equal(RoleNames.Member, result.Value.Role);
    }

    [Fact]
    public async Task Login_returns_the_same_message_for_unknown_email_and_wrong_password()
    {
        using var db = NewDb();
        await SeedUserAsync(db);
        var service = NewService(db);

        var unknownEmail = await service.LoginAsync(
            new LoginRequest { Identifier = "khong-ton-tai@gym.test", Password = Password }, default);
        var wrongPassword = await service.LoginAsync(
            new LoginRequest { Identifier = "a@gym.test", Password = "sai-mat-khau" }, default);

        // Chong user enumeration: hai nhanh KHONG duoc lo ra email nao co that.
        Assert.Equal(unknownEmail.ErrorCode, wrongPassword.ErrorCode);
        Assert.Equal(unknownEmail.ErrorMessage, wrongPassword.ErrorMessage);
        Assert.Equal(StatusCodes.Status401Unauthorized, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_locked_account_with_423()
    {
        using var db = NewDb();
        await SeedUserAsync(db, status: UserStatuses.Locked);

        var result = await NewService(db).LoginAsync(
            new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);

        Assert.False(result.Succeeded);
        Assert.Equal("ACCOUNT_LOCKED", result.ErrorCode);
        Assert.Equal(StatusCodes.Status423Locked, result.StatusCode);
    }

    // ---------- AC-03: khoa tam sau 5 lan sai trong cua so 15 phut ----------

    [Fact]
    public async Task Sixth_wrong_password_within_the_window_locks_the_account()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        for (var i = 0; i < 5; i++)
        {
            await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = "sai" }, default);
        }

        var sixth = await service.LoginAsync(
            new LoginRequest { Identifier = "a@gym.test", Password = "sai" }, default);

        Assert.Equal("TOO_MANY_ATTEMPTS", sixth.ErrorCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, sixth.StatusCode);

        var stored = await db.Users.FirstAsync(item => item.Id == user.Id);
        Assert.NotNull(stored.LockedUntil);
        Assert.True(stored.LockedUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task Correct_password_is_still_refused_while_the_temporary_lock_is_active()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);
        user.LockedUntil = DateTime.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var result = await NewService(db).LoginAsync(
            new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);

        Assert.Equal("TOO_MANY_ATTEMPTS", result.ErrorCode);
    }

    [Fact]
    public async Task Failed_counter_restarts_when_the_previous_window_has_expired()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);
        // Cua so truot: 4 lan sai nhung tu HON 15 phut truoc -> phai dem lai tu dau.
        user.FailedLoginCount = 4;
        user.LoginWindowStartedAt = DateTime.UtcNow.AddMinutes(-16);
        await db.SaveChangesAsync();

        await NewService(db).LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = "sai" }, default);

        var stored = await db.Users.FirstAsync(item => item.Id == user.Id);
        Assert.Equal(1, stored.FailedLoginCount);
        Assert.Null(stored.LockedUntil);
    }

    [Fact]
    public async Task Successful_login_clears_the_failed_counter()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = "sai" }, default);
        await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);

        var stored = await db.Users.FirstAsync(item => item.Id == user.Id);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LoginWindowStartedAt);
        Assert.NotNull(stored.LastLoginAt);
    }

    // ---------- AC-04/AC-05: rotate refresh token + logout ----------

    [Fact]
    public async Task Refresh_rotates_the_token_and_revokes_the_old_one()
    {
        using var db = NewDb();
        await SeedUserAsync(db);
        var service = NewService(db);

        var login = await service.LoginAsync(
            new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);
        var firstToken = login.Value!.RefreshToken;

        var refreshed = await service.RefreshAsync(new RefreshTokenRequest(firstToken), default);

        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(firstToken, refreshed.Value!.RefreshToken);

        // Token cu phai bi thu hoi ngay -> dung lai lan nua that bai.
        var reuseOld = await service.RefreshAsync(new RefreshTokenRequest(firstToken), default);
        Assert.False(reuseOld.Succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, reuseOld.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_every_refresh_token_of_the_user()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        var first = await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);
        var second = await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);

        var logout = await service.LogoutAsync(PrincipalFor(user), default);
        Assert.True(logout.Succeeded);

        foreach (var token in new[] { first.Value!.RefreshToken, second.Value!.RefreshToken })
        {
            var result = await service.RefreshAsync(new RefreshTokenRequest(token), default);
            Assert.False(result.Succeeded);
        }
    }

    [Fact]
    public async Task Refresh_rejects_a_token_that_does_not_exist()
    {
        using var db = NewDb();
        await SeedUserAsync(db);

        var result = await NewService(db).RefreshAsync(new RefreshTokenRequest("token-bia-dat"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // ---------- AC-06/AC-07: dang ky ----------

    [Fact]
    public async Task Register_creates_a_member_account_and_returns_tokens()
    {
        using var db = NewDb();
        db.Roles.Add(new Role { Id = 4, Name = RoleNames.Member });
        await db.SaveChangesAsync();

        var result = await NewService(db).RegisterAsync(
            new RegisterRequest("Nguoi Moi", "moi@gym.test", "0900000001", Password), default);

        Assert.True(result.Succeeded);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);

        var created = await db.Users.FirstAsync(item => item.Email == "moi@gym.test");
        // SEC-01: khong bao gio luu plaintext.
        Assert.NotEqual(Password, created.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, created.PasswordHash));
    }

    [Fact]
    public async Task Register_refuses_an_email_that_already_exists()
    {
        using var db = NewDb();
        await SeedUserAsync(db);

        var result = await NewService(db).RegisterAsync(
            new RegisterRequest("Trung Email", "a@gym.test", null, Password), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    // ---------- AC-08/AC-09/AC-09a: OTP quen mat khau ----------

    [Fact]
    public async Task Forgot_password_answers_the_same_way_for_an_unknown_email()
    {
        using var db = NewDb();
        await SeedUserAsync(db);
        var service = NewService(db);

        var known = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@gym.test"), default);
        var unknown = await service.ForgotPasswordAsync(new ForgotPasswordRequest("khong-co@gym.test"), default);

        // Khong duoc lo email nao ton tai.
        Assert.True(known.Succeeded);
        Assert.True(unknown.Succeeded);
    }

    [Fact]
    public async Task Reset_password_with_a_valid_otp_changes_the_password_and_revokes_refresh_tokens()
    {
        using var db = NewDb();
        await SeedUserAsync(db);
        var service = NewService(db);

        var login = await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);
        var forgot = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@gym.test"), default);
        var otp = forgot.Value!.ResetToken;
        Assert.False(string.IsNullOrWhiteSpace(otp)); // Development tra OTP de test duoc

        var reset = await service.ResetPasswordAsync(
            new ResetPasswordRequest("a@gym.test", otp!, "matkhaumoi456"), default);
        Assert.True(reset.Succeeded);

        // Mat khau moi dung, mat khau cu sai.
        var withNew = await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = "matkhaumoi456" }, default);
        Assert.True(withNew.Succeeded);

        // Refresh token cap truoc khi doi mat khau phai bi thu hoi.
        var oldToken = await service.RefreshAsync(new RefreshTokenRequest(login.Value!.RefreshToken), default);
        Assert.False(oldToken.Succeeded);
    }

    [Fact]
    public async Task Reset_password_invalidates_the_otp_after_three_wrong_attempts()
    {
        using var db = NewDb();
        await SeedUserAsync(db);
        var service = NewService(db);

        var forgot = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@gym.test"), default);
        var otp = forgot.Value!.ResetToken!;

        for (var i = 0; i < 3; i++)
        {
            var wrong = await service.ResetPasswordAsync(
                new ResetPasswordRequest("a@gym.test", "000000", "matkhaumoi456"), default);
            Assert.False(wrong.Succeeded);
        }

        // Sau 3 lan sai, ma DUNG cung khong dung duoc nua -> phai xin ma moi.
        var withCorrectOtp = await service.ResetPasswordAsync(
            new ResetPasswordRequest("a@gym.test", otp, "matkhaumoi456"), default);

        Assert.False(withCorrectOtp.Succeeded);
    }

    // ---------- AC-10/AC-11: doi mat khau ----------

    [Fact]
    public async Task Change_password_requires_the_current_password()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);

        var result = await NewService(db).ChangePasswordAsync(
            PrincipalFor(user), new ChangePasswordRequest("sai-mat-khau-cu", "matkhaumoi456"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task Change_password_revokes_refresh_tokens_issued_before_it()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        var login = await service.LoginAsync(new LoginRequest { Identifier = "a@gym.test", Password = Password }, default);

        var changed = await service.ChangePasswordAsync(
            PrincipalFor(user), new ChangePasswordRequest(Password, "matkhaumoi456"), default);
        Assert.True(changed.Succeeded);

        var oldToken = await service.RefreshAsync(new RefreshTokenRequest(login.Value!.RefreshToken), default);
        Assert.False(oldToken.Succeeded);
    }

    // ---------- AC-13: Google chua cau hinh ----------

    [Fact]
    public async Task Google_login_fails_clearly_when_client_id_is_not_configured()
    {
        using var db = NewDb();
        await SeedUserAsync(db);

        var result = await NewService(db).GoogleLoginAsync(new GoogleLoginRequest("id-token-bat-ky"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("GOOGLE_NOT_CONFIGURED", result.ErrorCode);
    }

    // ---------- Test double ----------

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<string> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add(toEmail);
            return Task.CompletedTask;
        }
    }

    // ForgotPasswordAsync chi tra OTP ra response khi dang o Development.
    private sealed class DevEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "GymMaster.API";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NullFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
        public IFileInfo GetFileInfo(string subpath) => new NotFoundFileInfo(subpath);
        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }
}
