using System.Security.Claims;
using GymMaster.API.DTOs;

namespace GymMaster.API.Services;

public interface IProgressService
{
    Task<AuthServiceResult<ProgressResponse>> RecordAsync(
        long memberId,
        RecordProgressRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<AuthServiceResult<IReadOnlyList<ProgressResponse>>> GetTimelineAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
