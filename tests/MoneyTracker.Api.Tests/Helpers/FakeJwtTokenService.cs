using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Auth;

namespace MoneyTracker.Api.Tests.Helpers;

public class FakeJwtTokenService : IJwtTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
        => ("access-token", DateTimeOffset.UtcNow.AddHours(1));

    public (string Token, string Hash, DateTimeOffset ExpiresAt) CreateRefreshToken()
        => ("refresh-token", "hash:refresh-token", DateTimeOffset.UtcNow.AddDays(7));

    public string HashRefreshToken(string token) => $"hash:{token}";
}
