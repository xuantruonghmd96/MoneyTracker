using System.ComponentModel.DataAnnotations;

namespace MoneyTracker.Api.Dtos.Auth;

public record RegisterRequest(
    [Required(ErrorMessage = "REQUIRED"), EmailAddress(ErrorMessage = "INVALID_EMAIL"), MaxLength(256, ErrorMessage = "TOO_LONG")] string Email,
    [Required(ErrorMessage = "REQUIRED"), MinLength(8, ErrorMessage = "TOO_SHORT"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Password,
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string DisplayName);

public record LoginRequest(
    [Required(ErrorMessage = "REQUIRED"), EmailAddress(ErrorMessage = "INVALID_EMAIL")] string Email,
    [Required(ErrorMessage = "REQUIRED")] string Password);

public record RefreshRequest([Required(ErrorMessage = "REQUIRED")] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse User);

public record UserResponse(Guid Id, string Email, string DisplayName);
