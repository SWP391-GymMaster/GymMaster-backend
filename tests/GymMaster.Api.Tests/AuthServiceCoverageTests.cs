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

// Bo sung coverage AuthService: Register (validate/duplicate), GetCurrentUser, ChangePassword,
// ForgotPassword (dev OTP + resend cooldown), ResetPassword (validate/invalid token).
public class AuthServiceCoverageTests
{
    private const string Password = "matkhau123";

    private static GymMasterDbContext NewDb()
        => new(new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"authcov-{Guid.NewGuid()}").Options);

    private static AuthService NewService(GymMasterDbContext db, RecordingEmailSender? mail = null)
    {
        var jwt = Options.Create(new JwtOptions
        {
            SecretKey = "khoa-bi-mat-du-dai-cho-hs256-toi-thieu-32-ky-tu",
            Issuer = "GymMaster",
            Audience = "GymMaster.Client",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        });
        return new AuthService(
            db, jwt,
            Options.Create(new GoogleAuthOptions()),
            new DevEnvironment(),
            mail ?? new RecordingEmailSender(),
            Options.Create(new EmailOptions()));
    }

    private static async Task<User> SeedUserAsync(GymMasterDbContext db, string email = "a@gym.test",
        string status = UserStatuses.Active, string? phone = null)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Member)
            ?? db.Roles.Add(new Role { Name = RoleNames.Member }).Entity;
        var user = new User
        {
            Email = email, Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 12),
            FullName = "Nguoi Dung", Status = status, CreatedAt = DateTime.UtcNow,
        };
        user.UserRoles.Add(new UserRole { Role = role, User = user });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static ClaimsPrincipal PrincipalFor(long userId)
        => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    // ---------- RegisterAsync ----------

    [Theory]
    [InlineData("", "e@gym.test", "matkhau123")]
    [InlineData("Ten", "", "matkhau123")]
    [InlineData("Ten", "e@gym.test", "")]
    public async Task Register_missing_fields_validation_error(string name, string email, string pw)
    {
        using var db = NewDb();
        var result = await NewService(db).RegisterAsync(new RegisterRequest(name, email, null, pw), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Register_short_password()
    {
        using var db = NewDb();
        var result = await NewService(db).RegisterAsync(new RegisterRequest("Ten", "e@gym.test", null, "123"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Register_duplicate_phone()
    {
        using var db = NewDb();
        await SeedUserAsync(db, email: "exist@gym.test", phone: "0987000001");

        var result = await NewService(db).RegisterAsync(
            new RegisterRequest("Ten", "new@gym.test", "0987000001", "matkhau123"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("PHONE_EXISTS", result.ErrorCode);
    }

    // ---------- GetCurrentUserAsync ----------

    [Fact]
    public async Task GetCurrentUser_returns_user()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);

        var result = await NewService(db).GetCurrentUserAsync(PrincipalFor(user.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(user.Email, result.Value!.Email);
    }

    [Fact]
    public async Task GetCurrentUser_unauthorized_when_no_token()
    {
        using var db = NewDb();
        var result = await NewService(db).GetCurrentUserAsync(Anonymous(), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_locked_returns_423()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db, status: UserStatuses.Locked);

        var result = await NewService(db).GetCurrentUserAsync(PrincipalFor(user.Id), default);

        Assert.False(result.Succeeded);
        Assert.Equal("ACCOUNT_LOCKED", result.ErrorCode);
        Assert.Equal(StatusCodes.Status423Locked, result.StatusCode);
    }

    // ---------- ChangePasswordAsync ----------

    [Fact]
    public async Task ChangePassword_success_with_correct_current()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);

        var result = await NewService(db).ChangePasswordAsync(
            PrincipalFor(user.Id), new ChangePasswordRequest(Password, "matkhaumoi1"), default);

        Assert.True(result.Succeeded);
        var saved = await db.Users.FindAsync(user.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("matkhaumoi1", saved!.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_short_new_password()
    {
        using var db = NewDb();
        var user = await SeedUserAsync(db);

        var result = await NewService(db).ChangePasswordAsync(
            PrincipalFor(user.Id), new ChangePasswordRequest(Password, "123"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task ChangePassword_unauthorized_when_no_user()
    {
        using var db = NewDb();
        var result = await NewService(db).ChangePasswordAsync(
            Anonymous(), new ChangePasswordRequest(Password, "matkhaumoi1"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    // ---------- ForgotPasswordAsync ----------

    [Fact] // Email ton tai, chua cau hinh SMTP, dang Development -> tra OTP
    public async Task Forgot_password_returns_otp_in_dev_for_known_email()
    {
        using var db = NewDb();
        await SeedUserAsync(db, email: "known@gym.test");

        var result = await NewService(db).ForgotPasswordAsync(new ForgotPasswordRequest("known@gym.test"), default);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Value!.ResetToken));
        Assert.Single(await db.PasswordResetTokens.ToListAsync());
    }

    [Fact] // Email rong -> VALIDATION_ERROR
    public async Task Forgot_password_empty_email_validation_error()
    {
        using var db = NewDb();
        var result = await NewService(db).ForgotPasswordAsync(new ForgotPasswordRequest("  "), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // Yeu cau lai trong 60s -> khong tao OTP moi (chong spam)
    public async Task Forgot_password_resend_cooldown_does_not_create_second_otp()
    {
        using var db = NewDb();
        await SeedUserAsync(db, email: "cool@gym.test");
        var svc = NewService(db);

        await svc.ForgotPasswordAsync(new ForgotPasswordRequest("cool@gym.test"), default);
        var second = await svc.ForgotPasswordAsync(new ForgotPasswordRequest("cool@gym.test"), default);

        Assert.True(second.Succeeded);
        Assert.Null(second.Value!.ResetToken); // khong tra OTP moi
        Assert.Single(await db.PasswordResetTokens.ToListAsync()); // van chi 1 token
    }

    // ---------- ResetPasswordAsync ----------

    [Fact]
    public async Task Reset_password_missing_fields_validation_error()
    {
        using var db = NewDb();
        var result = await NewService(db).ResetPasswordAsync(
            new ResetPasswordRequest("", "", ""), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task Reset_password_short_new_password()
    {
        using var db = NewDb();
        var result = await NewService(db).ResetPasswordAsync(
            new ResetPasswordRequest("a@gym.test", "123456", "123"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    [Fact] // Khong co OTP nao -> INVALID_RESET_TOKEN
    public async Task Reset_password_no_token_invalid()
    {
        using var db = NewDb();
        await SeedUserAsync(db, email: "noreq@gym.test");

        var result = await NewService(db).ResetPasswordAsync(
            new ResetPasswordRequest("noreq@gym.test", "999999", "matkhaumoi1"), default);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_RESET_TOKEN", result.ErrorCode);
    }

    [Fact] // OTP dung -> doi mat khau thanh cong
    public async Task Reset_password_with_valid_otp_changes_password()
    {
        using var db = NewDb();
        await SeedUserAsync(db, email: "reset@gym.test");
        var svc = NewService(db);
        var otp = (await svc.ForgotPasswordAsync(new ForgotPasswordRequest("reset@gym.test"), default)).Value!.ResetToken!;

        var result = await svc.ResetPasswordAsync(
            new ResetPasswordRequest("reset@gym.test", otp, "matkhaumoi9"), default);

        Assert.True(result.Succeeded);
        var user = await db.Users.SingleAsync(u => u.Email == "reset@gym.test");
        Assert.True(BCrypt.Net.BCrypt.Verify("matkhaumoi9", user.PasswordHash));
    }

    // ---------- RefreshAsync ----------

    [Fact]
    public async Task Refresh_missing_token_validation_error()
    {
        using var db = NewDb();
        var result = await NewService(db).RefreshAsync(new RefreshTokenRequest(""), default);

        Assert.False(result.Succeeded);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
    }

    // ---------- Test doubles ----------

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<string> Sent { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add(toEmail);
            return Task.CompletedTask;
        }
    }

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
