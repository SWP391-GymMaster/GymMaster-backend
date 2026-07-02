using Google.Apis.Auth;

namespace GymMaster.API.Services;

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    public async Task<GoogleTokenPayload> ValidateAsync(
        string idToken,
        string clientId,
        CancellationToken cancellationToken)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
                // Dung sai 5 phut chong lech dong ho may (tranh loi "JWT is not yet valid").
                IssuedAtClockTolerance = TimeSpan.FromMinutes(5),
                ExpirationTimeClockTolerance = TimeSpan.FromMinutes(5)
            });

        return new GoogleTokenPayload(
            payload.Email,
            payload.EmailVerified == true,
            payload.Name,
            payload.Picture);
    }
}
