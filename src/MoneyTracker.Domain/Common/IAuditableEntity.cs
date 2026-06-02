namespace MoneyTracker.Domain.Common;

public interface IAuditableEntity
{
    Guid Id { get; }
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
