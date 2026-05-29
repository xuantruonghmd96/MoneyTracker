using System.ComponentModel.DataAnnotations;
using MoneyTracker.Domain.Entities;

namespace MoneyTracker.Api.Dtos.Wallets;

public record CreateWalletRequest(
    Guid? Id,                                       // optional: client gen UUID cho offline-first
    [Required, MaxLength(128)] string Name,
    [Required] WalletType Type,
    decimal? CreditLimit,                           // bắt buộc nếu Type = Credit
    decimal? InitialBalance,
    [MaxLength(8)] string? Currency,
    [MaxLength(64)] string? Icon,
    [MaxLength(16)] string? Color);

public record UpdateWalletRequest(
    [Required, MaxLength(128)] string Name,
    decimal? CreditLimit,
    decimal? InitialBalance,
    [MaxLength(64)] string? Icon,
    [MaxLength(16)] string? Color);

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
