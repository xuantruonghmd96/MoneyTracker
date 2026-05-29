using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MoneyTracker.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MoneyTracker.Infrastructure.Auth;

public interface IJwtTokenService
{
    /// <summary>Tạo access token JWT cho user.</summary>
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);

    /// <summary>Tạo refresh token (random opaque) + trả về hash để lưu DB.</summary>
    (string Token, string Hash, DateTimeOffset ExpiresAt) CreateRefreshToken();

    /// <summary>Hash một refresh token để so sánh với DB.</summary>
    string HashRefreshToken(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
            throw new InvalidOperationException("JWT signing key must be at least 32 chars. Set Jwt:SigningKey.");
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }

    public (string Token, string Hash, DateTimeOffset ExpiresAt) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(bytes).Replace("/", "_").Replace("+", "-").TrimEnd('=');
        var hash = HashRefreshToken(token);
        var expires = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);
        return (token, hash, expires);
    }

    public string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
