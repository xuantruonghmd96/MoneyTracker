using System.ComponentModel.DataAnnotations;
using MoneyTracker.Domain.Entities;

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

public record CategoryRef(
    Guid Id,
    string Name,
    CategoryType Type,
    bool IsSystem,
    string? SystemKey,
    string? Icon,
    string? Color);

public record ParticipantRef(
    Guid Id,
    string Name,
    bool IsDefault);

public record TransactionResponse(
    Guid Id,
    decimal Amount,
    DateTimeOffset OccurredAt,
    Guid WalletId,
    string? Note,
    CategoryRef Category,
    ParticipantRef? Participant,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
