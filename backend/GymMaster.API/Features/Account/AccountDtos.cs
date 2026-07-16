namespace GymMaster.API.Features.Account;
public sealed record UpdateMyAccountRequest(
    string? FullName,
    string? Phone);

public sealed record PersonalProfileResponse(
    DateTime? DateOfBirth,
    string? Gender,
    string? Address,
    string? EmergencyContact);

public sealed record UpdatePersonalProfileRequest(
    DateTime? DateOfBirth,
    string? Gender,
    string? Address,
    string? EmergencyContact);
