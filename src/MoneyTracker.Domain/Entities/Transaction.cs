using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

public class Transaction : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid WalletId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? ParticipantId { get; set; }

    /// <summary>Số tiền dương. Dấu (+/-) được xác định bởi Category.Type (Income/Expense).</summary>
    public decimal Amount { get; set; }

    /// <summary>Thời điểm thực tế của giao dịch (do user chọn, không phải lúc nhập).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Nav
    public User? User { get; set; }
    public Wallet? Wallet { get; set; }
    public Category? Category { get; set; }
    public Participant? Participant { get; set; }
}
