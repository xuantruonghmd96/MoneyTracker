using MoneyTracker.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace MoneyTracker.Api.Auth;

public class CurrentActorContext : ICurrentActorContext
{
    public Guid? ActorUserId { get; }
    public string? ActorDeviceId { get; }

    public CurrentActorContext(ICurrentUser user, IHttpContextAccessor http)
    {
        ActorUserId = user.IsAuthenticated ? user.Id : null;
        ActorDeviceId = http.HttpContext?.Request.Headers["X-Device-Id"].FirstOrDefault();
    }
}
