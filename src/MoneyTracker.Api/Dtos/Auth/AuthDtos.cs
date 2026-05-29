using System.ComponentModel.DataAnnotations;

namespace MoneyTracker.Api.Dtos.Auth;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(128)] string DisplayName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse User);

public record UserResponse(Guid Id, string Email, string DisplayName);
