using MoneyTracker.Infrastructure.Auth;

namespace MoneyTracker.Api.Tests.Helpers;

public class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string password, string hash) => hash == $"hashed:{password}";
}
