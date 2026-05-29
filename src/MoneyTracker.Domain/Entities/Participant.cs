using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

public class Participant : ISyncEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>True cho participant "Ai đó" được tạo tự động lúc register. Không xóa được.</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Nav
    public User? User { get; set; }
}
