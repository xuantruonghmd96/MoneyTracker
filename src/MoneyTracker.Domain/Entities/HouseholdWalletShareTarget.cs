namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Target members cụ thể khi HouseholdWalletShare.SharedWithAllMembers = false (iter 3).
/// </summary>
public class HouseholdWalletShareTarget
{
    public Guid Id { get; set; }
    public Guid ShareId { get; set; }
    public Guid TargetHouseholdMemberId { get; set; }

    // Nav
    public HouseholdWalletShare? Share { get; set; }
    public HouseholdMember? TargetHouseholdMember { get; set; }
}
