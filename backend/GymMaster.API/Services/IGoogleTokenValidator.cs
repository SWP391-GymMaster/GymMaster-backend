namespace GymMaster.API.Services;

public sealed record GoogleTokenPayload(
    string? Email,
    bool EmailVerified,
    string? Name,
    string? Picture);

public interface IGoogleTokenValidator
{
    Task<GoogleTokenPayload> ValidateAsync(
        string idToken,
        string clientId,
        CancellationToken cancellationToken);
}
