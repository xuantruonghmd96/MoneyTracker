using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

public enum CategoryType
{
    Income = 0,
    Expense = 1,
    Debt = 2
}

public class Category : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType Type { get; set; }

    /// <summary>Danh mục cha. Null = top-level.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Chỉ set khi UserId IS NULL (system category). Ví dụ: "DEBT_LEND".</summary>
    public string? SystemKey { get; set; }

    /// <summary>
    /// Nếu true, danh mục này tự động xuất hiện trong tất cả ví hiện tại và tương lai
    /// (không cần row trong WalletCategories). Nếu false, chỉ xuất hiện ở các ví được assign
    /// qua WalletCategories.
    /// </summary>
    public bool AppliesToAllWallets { get; set; }

    public string? Icon { get; set; }
    public string? Color { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Nav
    public User? User { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<WalletCategory> WalletCategories { get; set; } = new List<WalletCategory>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
