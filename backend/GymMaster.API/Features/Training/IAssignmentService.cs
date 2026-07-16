using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Training;
public interface IAssignmentService
{
    Task<ServiceResult<TrainerAssignmentResponse>> AssignAsync(
        AssignTrainerRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<AssignedMemberResponse>>> GetAssignedMembersAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<AssignmentCandidateListResponse<AssignmentCandidateMemberResponse>>> GetCandidateMembersAsync(
        string? query,
        bool includeAssigned,
        CancellationToken cancellationToken);

    Task<ServiceResult<AssignmentCandidateListResponse<AssignmentCandidateTrainerResponse>>> GetCandidateTrainersAsync(
        string? query,
        string? specialty,
        CancellationToken cancellationToken);
}
