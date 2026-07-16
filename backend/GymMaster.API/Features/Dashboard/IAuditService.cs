namespace GymMaster.API.Features.Dashboard;
public interface IAuditService
{
    // AUDIT-01: ghi log hanh dong mutating. Metadata KHONG chua PII nhay cam (NFR-03).
    Task LogAsync(string action, string entity, long entityId, object? metadata, CancellationToken cancellationToken);
}
