using System.ComponentModel.DataAnnotations;

namespace MoneyTracker.Api.Dtos.Transactions;

public record CreateTransactionRequest(
    Guid? Id,
    [Required(ErrorMessage = "REQUIRED")] decimal? Amount,
    [Required(ErrorMessage = "REQUIRED")] DateTimeOffset? OccurredAt,
    [Required(ErrorMessage = "REQUIRED")] Guid? WalletId,
    [Required(ErrorMessage = "REQUIRED")] Guid? CategoryId,
    Guid? ParticipantId,
    [MaxLength(2048, ErrorMessage = "TOO_LONG")] string? Note);

public record UpdateTransactionRequest(
    [Required(ErrorMessage = "REQUIRED")] decimal? Amount,
    [Required(ErrorMessage = "REQUIRED")] DateTimeOffset? OccurredAt,
    [Required(ErrorMessage = "REQUIRED")] Guid? WalletId,
    [Required(ErrorMessage = "REQUIRED")] Guid? CategoryId,
    Guid? ParticipantId,
    [MaxLength(2048, ErrorMessage = "TOO_LONG")] string? Note);

public record TransactionResponse(
    Guid Id,
    decimal Amount,
    DateTimeOffset OccurredAt,
    Guid CategoryId,
    Guid WalletId,
    Guid? ParticipantId,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
