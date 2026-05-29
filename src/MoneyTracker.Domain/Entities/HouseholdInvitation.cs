namespace MoneyTracker.Domain.Entities;

public enum HouseholdInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
    Revoked = 4
}

public class HouseholdInvitation
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid InviterUserId { get; set; }

    /// <summary>Mã invite 8-12 ký tự, share qua link/QR.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional: invite theo email cụ thể.</summary>
    public string? InviteeEmail { get; set; }

    public HouseholdInvitationStatus Status { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public Household? Household { get; set; }
    public User? InviterUser { get; set; }
}
