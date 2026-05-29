namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Append-only audit trail cho mọi thao tác tạo/sửa/xóa Transaction.
/// Không có DeletedAt — không bao giờ xóa row.
/// TransactionId không có FK (giữ được khi transaction bị soft-delete sau này).
/// </summary>
public class TransactionAudit
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid UserId { get; set; }
    public string Operation { get; set; } = string.Empty;  // "create" | "update" | "delete"
    public string SnapshotJson { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string? ActorDevice { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
