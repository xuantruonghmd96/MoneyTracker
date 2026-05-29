using MoneyTracker.Api.Auth;

namespace MoneyTracker.Api.Tests.Helpers;

public class FakeCurrentUser(Guid id) : ICurrentUser
{
    public Guid Id => id;
    public bool IsAuthenticated => true;
}
