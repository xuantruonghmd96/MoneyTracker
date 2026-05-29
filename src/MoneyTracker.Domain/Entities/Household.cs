namespace MoneyTracker.Domain.Entities;

public class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
    public ICollection<HouseholdInvitation> Invitations { get; set; } = new List<HouseholdInvitation>();
}
