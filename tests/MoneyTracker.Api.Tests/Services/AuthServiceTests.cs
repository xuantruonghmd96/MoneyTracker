using FluentAssertions;
using MoneyTracker.Api.Dtos.Auth;
using MoneyTracker.Api.Services;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Api.Tests.Helpers;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new AuthService(_db, new FakeJwtTokenService(), new FakePasswordHasher());
    }

    public void Dispose() => _db.Dispose();

    // ===== Register =====

    [Fact]
    public async Task Register_NewEmail_CreatesUserAndDefaultParticipant()
    {
        var req = new RegisterRequest("Test@Example.COM", "password123", "Test User");

        await _sut.RegisterAsync(req, null, default);

        var user = _db.Users.Single();
        user.Email.Should().Be("test@example.com");
        user.DisplayName.Should().Be("Test User");
        user.PasswordHash.Should().Be("hashed:password123");

        var participant = _db.Participants.Single();
        participant.UserId.Should().Be(user.Id);
        participant.IsDefault.Should().BeTrue();
        participant.Name.Should().Be("Ai đó");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        _db.Users.Add(new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "x", DisplayName = "x" });
        await _db.SaveChangesAsync();

        var req = new RegisterRequest("TEST@EXAMPLE.COM", "password123", "Another");

        var act = () => _sut.RegisterAsync(req, null, default);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.EmailTaken);
    }

    [Fact]
    public async Task Register_ReturnsAuthResponseWithTokens()
    {
        var req = new RegisterRequest("user@example.com", "password123", "User");

        var result = await _sut.RegisterAsync(req, null, default);

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.User.Email.Should().Be("user@example.com");
    }

    // ===== Login =====

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Email = "login@example.com",
            PasswordHash = "hashed:secret", DisplayName = "Login User"
        });
        await _db.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest("login@example.com", "secret"), null, default);

        result.AccessToken.Should().Be("access-token");
        result.User.Email.Should().Be("login@example.com");
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsValidation()
    {
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Email = "login@example.com",
            PasswordHash = "hashed:correct", DisplayName = "x"
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.LoginAsync(new LoginRequest("login@example.com", "wrong"), null, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Login_UnknownEmail_ThrowsValidation()
    {
        var act = () => _sut.LoginAsync(new LoginRequest("nobody@example.com", "pass"), null, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.InvalidCredentials);
    }

    // ===== Refresh =====

    [Fact]
    public async Task Refresh_ValidActiveToken_RotatesToken()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "r@example.com", PasswordHash = "x", DisplayName = "x" };
        var oldToken = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            TokenHash = "hash:old-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(oldToken);
        await _db.SaveChangesAsync();

        var result = await _sut.RefreshAsync("old-token", null, default);

        result.AccessToken.Should().Be("access-token");
        oldToken.RevokedAt.Should().NotBeNull();
        _db.RefreshTokens.Count().Should().Be(2); // old + new
    }

    [Fact]
    public async Task Refresh_RevokedToken_ThrowsValidation()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "r2@example.com", PasswordHash = "x", DisplayName = "x" };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            TokenHash = "hash:revoked-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        var act = () => _sut.RefreshAsync("revoked-token", null, default);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(ErrorCodes.InvalidRefreshToken);
    }

    // ===== Logout =====

    [Fact]
    public async Task Logout_ActiveToken_RevokesIt()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "lo@example.com", PasswordHash = "x", DisplayName = "x" };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            TokenHash = "hash:my-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        await _sut.LogoutAsync("my-token", default);

        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Logout_UnknownToken_NoOp()
    {
        var act = () => _sut.LogoutAsync("nonexistent-token", default);

        await act.Should().NotThrowAsync();
    }
}
