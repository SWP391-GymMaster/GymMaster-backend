using System.Security.Claims;
using System.Text.Json;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Features.Dashboard;
public sealed class AuditService : IAuditService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(GymMasterDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entity,
        long entityId,
        object? metadata,
        CancellationToken cancellationToken)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = GetActorId(),
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private long? GetActorId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }
}
