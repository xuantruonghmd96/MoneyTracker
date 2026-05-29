using System.Security.Claims;

namespace MoneyTracker.Api.Auth;

public interface ICurrentUser
{
    Guid Id { get; }
    bool IsAuthenticated { get; }
}

public class CurrentUser : ICurrentUser
{
    public Guid Id { get; }
    public bool IsAuthenticated { get; }

    public CurrentUser(IHttpContextAccessor accessor)
    {
        var sub = accessor.HttpContext?.User.FindFirstValue("sub")
            ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id))
        {
            Id = id;
            IsAuthenticated = true;
        }
    }
}
