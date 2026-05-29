namespace MoneyTracker.Domain.Common;

public interface ICurrentActorContext
{
    Guid? ActorUserId { get; }
    string? ActorDeviceId { get; }
}
