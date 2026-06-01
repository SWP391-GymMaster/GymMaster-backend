namespace GymMaster.API.DTOs;

// Spec 002 — Trainer profile (FR-PT-PROF-01)

public sealed record CreateTrainerRequest(
    long UserId,
    string? Specialty,
    string? Bio,
    string? Gender,
    DateTime? DateOfBirth,
    int? YearsOfExperience);

public sealed record UpdateTrainerRequest(
    string? Specialty,
    string? Bio,
    string? Gender,
    DateTime? DateOfBirth,
    int? YearsOfExperience);

public sealed record TrainerResponse(
    long Id,
    long UserId,
    string Email,
    string FullName,
    string? Specialty,
    string? Bio,
    string? Gender,
    DateTime? DateOfBirth,
    int? YearsOfExperience,
    DateTime CreatedAt);
