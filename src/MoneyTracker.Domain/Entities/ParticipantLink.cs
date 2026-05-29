namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Pair-wise symmetric link giữa 2 participants (dùng cho cross-member debt aggregation - iter 3).
/// Schema only trong iter 2.
/// </summary>
public class ParticipantLink
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid ParticipantAId { get; set; }
    public Guid ParticipantBId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
