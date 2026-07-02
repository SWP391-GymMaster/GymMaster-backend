namespace GymMaster.API.DTOs;

public sealed record UpdateMyAccountRequest(
    string? FullName,
    string? Phone);
