using System.Security.Claims;
using GymMaster.API.Common;
namespace GymMaster.API.Features.Training;
public interface IProgressService
{
    Task<ServiceResult<ProgressResponse>> RecordAsync(
        long memberId,
        RecordProgressRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ProgressResponse>>> GetTimelineAsync(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<ServiceResult<Profile360Response>> GetProfile360Async(
        long memberId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
