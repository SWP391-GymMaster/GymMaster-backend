using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IAssignmentService
{
    Task<AuthServiceResult<TrainerAssignmentResponse>> AssignAsync(
        AssignTrainerRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<AssignedMemberResponse>>> GetAssignedMembersAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<AssignmentCandidateListResponse<AssignmentCandidateMemberResponse>>> GetCandidateMembersAsync(
        string? query,
        bool includeAssigned,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<AssignmentCandidateListResponse<AssignmentCandidateTrainerResponse>>> GetCandidateTrainersAsync(
        string? query,
        string? specialty,
        CancellationToken cancellationToken);
}
