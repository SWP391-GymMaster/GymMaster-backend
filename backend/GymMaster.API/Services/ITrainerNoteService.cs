using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface ITrainerNoteService
{
    Task<AuthServiceResult<TrainerNoteResponse>> CreateAsync(
        long memberId,
        CreateTrainerNoteRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<TrainerNoteResponse>>> GetByMemberAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<TrainerNoteResponse>> UpdateAsync(
        long noteId,
        UpdateTrainerNoteRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<object?>> DeleteAsync(
        long noteId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
