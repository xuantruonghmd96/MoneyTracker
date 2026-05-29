namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Lưu lại kết quả của một sync push để đảm bảo idempotency.
/// Khi client retry cùng batchId, server trả lại ResponseJson đã cache.
/// </summary>
public class SyncBatch
{
    public Guid Id { get; set; }          // batchId từ client = PK
    public Guid UserId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
}
