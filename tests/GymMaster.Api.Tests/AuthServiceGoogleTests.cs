using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Options;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymMaster.Api.Tests;

public class AuthServiceGoogleTests
{
    private static GymMasterDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<GymMasterDbContext>()
            .UseInMemoryDatabase($"gym-{Guid.NewGuid()}")
            .Options;
        return new GymMasterDbContext(options);
    }

    [Fact]
    public async Task GoogleLogin_new_user_seeds_avatar_url_from_picture()
    {
        using var db = NewDb();
        var validator = new FakeGoogleTokenValidator(new GoogleTokenPayload(
            "google@gymmaster.local",
            true,
            "Google Member",
            "https://lh3.googleusercontent.com/avatar.jpg"));

        var service = new AuthService(
            db,
            Options.Create(new JwtOptions
            {
                Issuer = "GymMaster",
                Audience = "GymMaster.Client",
                SecretKey = "test-secret-key-with-enough-length-1234567890",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7
            }),
            Options.Create(new GoogleAuthOptions { ClientId = "google-client-id" }),
            validator,
            new TestEnvironment(),
            new NoopEmailSender(),
            Options.Create(new EmailOptions()));

        var result = await service.GoogleLoginAsync(new GoogleLoginRequest("token"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("https://lh3.googleusercontent.com/avatar.jpg", result.Value!.User.AvatarUrl);
        Assert.Equal("https://lh3.googleusercontent.com/avatar.jpg", (await db.Users.SingleAsync()).AvatarUrl);
        Assert.Equal("google-client-id", validator.ClientId);
    }

    private sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
    {
        private readonly GoogleTokenPayload _payload;

        public FakeGoogleTokenValidator(GoogleTokenPayload payload)
        {
            _payload = payload;
        }

        public string? ClientId { get; private set; }

        public Task<GoogleTokenPayload> ValidateAsync(
            string idToken,
            string clientId,
            CancellationToken cancellationToken)
        {
            ClientId = clientId;
            return Task.FromResult(_payload);
        }
    }

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "GymMaster.Api.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
