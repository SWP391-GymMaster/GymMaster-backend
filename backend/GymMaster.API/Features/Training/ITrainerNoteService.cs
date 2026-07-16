using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Training;
public interface ITrainerNoteService
{
    Task<ServiceResult<TrainerNoteResponse>> CreateAsync(
        long memberId,
        CreateTrainerNoteRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<TrainerNoteResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<TrainerNoteResponse>> UpdateAsync(
        long noteId,
        UpdateTrainerNoteRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<object?>> DeleteAsync(
        long noteId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
