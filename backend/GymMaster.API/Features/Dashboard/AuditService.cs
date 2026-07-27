using System.Security.Claims;
using System.Text.Json;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Features.Dashboard;

/// <summary>
/// Implementation luồng nội bộ 14: ghi lịch sử cho mọi thao tác thay đổi dữ liệu quan trọng.
/// Service lấy actor từ JWT, serialize metadata và lưu entity AuditLog.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(GymMasterDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    // LUỒNG NỘI BỘ 14 — được service nghiệp vụ gọi sau khi SaveChanges thành công.
    // action: tên hành động; entity/entityId: đối tượng bị tác động;
    // metadata: thông tin truy vết bổ sung, được serialize JSON và không chứa PII nhạy cảm.
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

    // Ưu tiên claim NameIdentifier; fallback "sub".
    // Nếu không có hoặc không phải số thì trả null nhưng Audit Log vẫn được ghi.
    private long? GetActorId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }
}
