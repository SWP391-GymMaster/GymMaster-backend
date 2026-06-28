namespace GymMaster.API.DTOs;

// Spec 005 — PT Assignment, Workout Plan & Trainer Notes

// --- Assignment ---

public sealed record AssignTrainerRequest(long MemberId, long TrainerId);

public sealed record AssignmentCandidateMemberResponse(
    long Id,
    string MemberCode,
    string FullName,
    string Email,
    string? Phone,
    string MembershipStatus,
    string? Goal,
    long? CurrentTrainerId,
    string? CurrentTrainerName,
    string Priority);

public sealed record AssignmentCandidateTrainerResponse(
    long Id,
    long UserId,
    string FullName,
    string Status,
    string? Specialty,
    int AssignedCount,
    int Capacity);

public sealed record AssignmentCandidateListResponse<T>(
    IReadOnlyList<T> Items,
    int Total);

public sealed record TrainerAssignmentResponse(
    long Id,
    long MemberId,
    string MemberName,
    long TrainerId,
    string TrainerName,
    int Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTime CreatedAt);

// Shape khop voi FE PtAssignedMember type.
public sealed record AssignedMemberResponse(
    long Id,
    string MemberCode,
    string FullName,
    string Email,
    string? Phone,
    string Status,
    DateOnly StartDate);

// --- Workout Plan ---

// FE sends exercise name; service looks up or creates catalog entry.
public sealed record WorkoutExerciseRequest(
    string Name,
    byte? Sets,
    string? Reps,
    string? Note,
    short OrderIndex);

public sealed record CreateWorkoutPlanRequest(
    string Title,
    string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<WorkoutExerciseRequest> Exercises);

public sealed record UpdateWorkoutPlanRequest(
    string? Title,
    string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? Status,
    IReadOnlyList<WorkoutExerciseRequest>? Exercises);

// Field names aligned with FE WorkoutExercise type.
public sealed record WorkoutExerciseResponse(
    long Id,
    string Name,
    string? MuscleGroup,
    short OrderIndex,
    byte? Sets,
    string? Reps,
    string? Note);

public sealed record WorkoutPlanResponse(
    long Id,
    long MemberId,
    string MemberName,
    long TrainerId,
    string TrainerName,
    string Title,
    string? Goal,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<WorkoutExerciseResponse> Exercises);

// --- Trainer Note ---

public sealed record CreateTrainerNoteRequest(string Content);

public sealed record UpdateTrainerNoteRequest(string Content);

public sealed record TrainerNoteResponse(
    long Id,
    long MemberId,
    string MemberName,
    long TrainerId,
    string TrainerName,
    DateOnly NoteDate,
    string Content,
    DateTime CreatedAt);

// --- Exercise Catalog ---

public sealed record ExerciseCatalogResponse(
    long Id,
    string Name,
    string? MuscleGroup,
    string? Description);
