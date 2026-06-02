using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Share rule cho wallet trong household (schema only - iter 2, endpoints iter 3).
/// Pattern y hệt HouseholdCategoryShare.
/// </summary>
public class HouseholdWalletShare : IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public Guid WalletId { get; set; }
    public bool SharedWithAllMembers { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Nav
    public HouseholdMember? HouseholdMember { get; set; }
    public Wallet? Wallet { get; set; }
    public ICollection<HouseholdWalletShareTarget> Targets { get; set; } = new List<HouseholdWalletShareTarget>();
}
