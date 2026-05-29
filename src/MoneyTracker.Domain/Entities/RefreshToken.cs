namespace MoneyTracker.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;
}
