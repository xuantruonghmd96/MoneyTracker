namespace MoneyTracker.Domain.Common;

/// <summary>
/// Marker for entities that participate in offline-first sync.
/// Sync cursor is UpdatedAt (microsecond precision in Postgres).
/// DeletedAt = soft-delete tombstone (sync needs to push deletes to clients).
/// </summary>
public interface ISyncEntity : IAuditableEntity
{
    Guid? UserId { get; }
    DateTimeOffset? DeletedAt { get; set; }
}
