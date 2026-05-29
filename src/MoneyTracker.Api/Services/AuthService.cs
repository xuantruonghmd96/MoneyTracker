using MoneyTracker.Api.Dtos.Auth;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Auth;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;

    public AuthService(AppDbContext db, IJwtTokenService jwt, IPasswordHasher hasher)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, string? ipAddress, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException(ErrorCodes.EmailTaken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(req.Password),
            DisplayName = req.DisplayName.Trim()
        };
        _db.Users.Add(user);

        _db.Participants.Add(new Participant
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Ai đó",
            IsDefault = true
        });

        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ipAddress, null, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, string? ipAddress, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user == null || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new ValidationException(ErrorCodes.InvalidCredentials);

        return await IssueTokensAsync(user, ipAddress, null, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string rawToken, string? ipAddress, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(rawToken);
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token == null || !token.IsActive || token.User == null)
            throw new ValidationException(ErrorCodes.InvalidRefreshToken);

        token.RevokedAt = DateTimeOffset.UtcNow;
        var response = await IssueTokensAsync(token.User, ipAddress, token, ct);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string? rawToken, CancellationToken ct)
    {
        if (rawToken == null) return;
        var hash = _jwt.HashRefreshToken(rawToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string? ipAddress, RefreshToken? previous, CancellationToken ct)
    {
        var (accessJwt, accessExp) = _jwt.CreateAccessToken(user);
        var (refresh, refreshHash, refreshExp) = _jwt.CreateRefreshToken();

        var refreshEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExp,
            CreatedByIp = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (previous != null)
            previous.ReplacedByTokenId = refreshEntity.Id;

        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            accessJwt, accessExp,
            refresh, refreshExp,
            new UserResponse(user.Id, user.Email, user.DisplayName));
    }
}
