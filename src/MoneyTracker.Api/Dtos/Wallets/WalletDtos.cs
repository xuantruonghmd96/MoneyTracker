using System.ComponentModel.DataAnnotations;
using MoneyTracker.Domain.Entities;

namespace MoneyTracker.Api.Dtos.Wallets;

public record CreateWalletRequest(
    Guid? Id,                                       // optional: client gen UUID cho offline-first
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Name,
    [Required(ErrorMessage = "REQUIRED")] WalletType Type,
    decimal? CreditLimit,                           // bắt buộc nếu Type = Credit
    decimal? InitialBalance,
    [MaxLength(8, ErrorMessage = "TOO_LONG")] string? Currency,
    [MaxLength(64, ErrorMessage = "TOO_LONG")] string? Icon,
    [MaxLength(16, ErrorMessage = "TOO_LONG")] string? Color);

public record UpdateWalletRequest(
    [Required(ErrorMessage = "REQUIRED"), MaxLength(128, ErrorMessage = "TOO_LONG")] string Name,
    decimal? CreditLimit,
    decimal? InitialBalance,
    [MaxLength(64, ErrorMessage = "TOO_LONG")] string? Icon,
    [MaxLength(16, ErrorMessage = "TOO_LONG")] string? Color);

public record WalletResponse(
    Guid Id,
    string Name,
    WalletType Type,
    decimal? CreditLimit,
    decimal InitialBalance,
    string Currency,
    string? Icon,
    string? Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
