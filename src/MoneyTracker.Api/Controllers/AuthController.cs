using MoneyTracker.Api.Common;
using MoneyTracker.Api.Dtos.Auth;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Auth;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;

    public AuthController(AppDbContext db, IJwtTokenService jwt, IPasswordHasher hasher)
    {
        _db = db;
        _jwt = jwt;
        _hasher = hasher;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new ApiError(ErrorCodes.EmailTaken));

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(req.Password),
            DisplayName = req.DisplayName.Trim()
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !_hasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new ApiError(ErrorCodes.InvalidCredentials));

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var hash = _jwt.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token == null || !token.IsActive || token.User == null)
            return Unauthorized(new ApiError(ErrorCodes.InvalidRefreshToken));

        // Rotate: revoke cũ, cấp mới
        token.RevokedAt = DateTimeOffset.UtcNow;
        var response = await IssueTokensAsync(token.User, token);
        await _db.SaveChangesAsync();
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        var hash = _jwt.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, RefreshToken? previous = null)
    {
        var (accessJwt, accessExp) = _jwt.CreateAccessToken(user);
        var (refresh, refreshHash, refreshExp) = _jwt.CreateRefreshToken();

        var refreshEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExp,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (previous != null)
            previous.ReplacedByTokenId = refreshEntity.Id;

        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync();

        return new AuthResponse(
            accessJwt, accessExp,
            refresh, refreshExp,
            new UserResponse(user.Id, user.Email, user.DisplayName));
    }
}
