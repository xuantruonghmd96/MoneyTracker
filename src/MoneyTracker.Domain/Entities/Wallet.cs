using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

public enum WalletType
{
    Regular = 0,
    Credit = 1
}

public class Wallet : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public WalletType Type { get; set; }

    /// <summary>Hạn mức cho thẻ tín dụng. Null cho ví thường.</summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>Số dư đầu kỳ (opening balance). Số dư hiện tại = InitialBalance + sum(transactions).</summary>
    public decimal InitialBalance { get; set; }

    public string Currency { get; set; } = "VND";
    public string? Icon { get; set; }
    public string? Color { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Nav
    public User? User { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<WalletCategory> WalletCategories { get; set; } = new List<WalletCategory>();
}
