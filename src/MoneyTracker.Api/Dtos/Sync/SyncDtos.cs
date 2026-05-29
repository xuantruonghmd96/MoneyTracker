using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Api.Dtos.Transactions;
using MoneyTracker.Api.Dtos.Wallets;

namespace MoneyTracker.Api.Dtos.Sync;

public record SyncChangeItem(
    [Required(ErrorMessage = "REQUIRED")] Guid Id,
    [Required(ErrorMessage = "REQUIRED")] string Op,    // "upsert" | "delete"
    DateTimeOffset UpdatedAt,
    JsonElement? Data);                                  // null khi op="delete"

public record SyncPushChanges(
    List<SyncChangeItem>? Wallets,
    List<SyncChangeItem>? Categories,
    List<SyncChangeItem>? WalletCategories,
    List<SyncChangeItem>? Participants,
    List<SyncChangeItem>? Transactions);

public record SyncPushRequest(
    [Required(ErrorMessage = "REQUIRED")] Guid BatchId,
    string? DeviceId,
    [Required(ErrorMessage = "REQUIRED")] SyncPushChanges Changes);

public record SyncItemResult(
    Guid Id,
    string Status,              // "applied" | "skipped" | "rejected"
    DateTimeOffset? ServerUpdatedAt,
    string? ErrorCode);

public record SyncPushResults(
    List<SyncItemResult>? Wallets,
    List<SyncItemResult>? Categories,
    List<SyncItemResult>? WalletCategories,
    List<SyncItemResult>? Participants,
    List<SyncItemResult>? Transactions);

public record SyncPushResponse(
    SyncPushResults Results,
    DateTimeOffset ServerNow);

public record SyncPullResponse(
    List<WalletResponse> Wallets,
    List<CategoryResponse> Categories,
    List<WalletCategoryAssignmentResponse> WalletCategories,
    List<ParticipantResponse> Participants,
    List<TransactionResponse> Transactions,
    DateTimeOffset ServerNow);
