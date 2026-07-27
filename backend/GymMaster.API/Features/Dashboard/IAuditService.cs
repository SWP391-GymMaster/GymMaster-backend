namespace GymMaster.API.Features.Dashboard;

/// <summary>
/// Hợp đồng ghi Audit Log dùng chung cho mọi feature có thao tác thay đổi dữ liệu.
/// Interface thuộc phần Dashboard/Audit của Minh nhưng được service của cả nhóm gọi.
/// </summary>
public interface IAuditService
{
    // LUỒNG NỘI BỘ 14: ghi actor, action, entity, entityId, metadata và thời gian.
    // Metadata chỉ chứa thông tin truy vết cần thiết, không chứa PII nhạy cảm.
    Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken cancellationToken);
}
