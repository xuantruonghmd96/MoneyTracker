using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Junction giữa Wallet và Category. Có Id riêng (UUID) để sync được.
/// Chỉ tồn tại nếu Category.AppliesToAllWallets = false (lúc đó assign chọn lọc).
/// </summary>
public class WalletCategory : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    public Guid CategoryId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Nav
    public User? User { get; set; }
    public Wallet? Wallet { get; set; }
    public Category? Category { get; set; }
}
