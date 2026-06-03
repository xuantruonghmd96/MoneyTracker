using System.Text.Json;
using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Api.Dtos.Sync;
using MoneyTracker.Api.Dtos.Transactions;
using MoneyTracker.Api.Dtos.Wallets;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Services;

public class SyncService
{
    private readonly AppDbContext _db;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SyncService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SyncPushResponse> PushAsync(Guid userId, SyncPushRequest req, CancellationToken ct)
    {
        var existing = await _db.SyncBatches
            .FirstOrDefaultAsync(b => b.Id == req.BatchId && b.UserId == userId, ct);
        if (existing != null)
            return JsonSerializer.Deserialize<SyncPushResponse>(existing.ResponseJson, JsonOpts)!;

        var results = new SyncPushResults(
            new List<SyncItemResult>(), new List<SyncItemResult>(),
            new List<SyncItemResult>(), new List<SyncItemResult>(),
            new List<SyncItemResult>());

        var errors = new List<string>();

        await ProcessParticipants(userId, req.Changes.Participants, results.Participants!, errors, ct);
        await ProcessWallets(userId, req.Changes.Wallets, results.Wallets!, errors, ct);
        await ProcessWalletCategories(userId, req.Changes.WalletCategories, results.WalletCategories!, errors, ct);
        await ProcessCategories(userId, req.Changes.Categories, results.Categories!, errors, ct);
        await ProcessTransactions(userId, req.Changes.Transactions, results.Transactions!, errors, ct);

        if (errors.Count > 0)
            throw new ValidationException(ErrorCodes.SyncBatchRejected,
                errors.Select((e, i) => (Key: i.ToString(), Value: e))
                      .ToDictionary(x => x.Key, x => x.Value));

        var serverNow = DateTimeOffset.UtcNow;
        var response = new SyncPushResponse(results, serverNow);
        var responseJson = JsonSerializer.Serialize(response, JsonOpts);

        _db.SyncBatches.Add(new SyncBatch
        {
            Id = req.BatchId,
            UserId = userId,
            ProcessedAt = serverNow,
            ResponseJson = responseJson
        });

        await _db.SaveChangesAsync(ct);

        return response;
    }

    public async Task<SyncPullResponse> PullAsync(Guid userId, DateTimeOffset? since, CancellationToken ct)
    {
        var serverNow = DateTimeOffset.UtcNow;

        var wallets = await _db.Wallets
            .Where(w => w.UserId == userId && (since == null || w.UpdatedAt > since))
            .Select(w => new WalletResponse(
                w.Id, w.Name, w.Type, w.CreditLimit, w.InitialBalance,
                w.Currency, w.Icon, w.Color, w.CreatedAt, w.UpdatedAt))
            .ToListAsync(ct);

        var categories = await _db.Categories
            .ForUserIncludingSystem(userId)
            .Where(c => since == null || c.UpdatedAt > since)
            .Select(c => new CategoryResponse(
                c.Id, c.Name, c.Type, c.ParentId, c.AppliesToAllWallets,
                c.Icon, c.Color, c.UserId == null, c.SystemKey,
                c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);

        var walletCategories = await _db.WalletCategories
            .Where(wc => wc.UserId == userId && (since == null || wc.UpdatedAt > since))
            .Select(wc => new WalletCategoryAssignmentResponse(wc.Id, wc.WalletId, wc.CategoryId, wc.CreatedAt))
            .ToListAsync(ct);

        var participants = await _db.Participants
            .Where(p => p.UserId == userId && (since == null || p.UpdatedAt > since))
            .Select(p => new ParticipantResponse(p.Id, p.Name, p.Note, p.IsDefault, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);

        var transactions = await _db.Transactions
            .Where(t => t.UserId == userId && (since == null || t.UpdatedAt > since))
            .Select(t => new TransactionResponse(
                t.Id, t.Amount, t.OccurredAt, t.CategoryId, t.WalletId, t.ParticipantId,
                t.Note, t.CreatedAt, t.UpdatedAt, t.DeletedAt))
            .ToListAsync(ct);

        return new SyncPullResponse(wallets, categories, walletCategories, participants, transactions, serverNow);
    }

    // ===== Push helpers =====

    private async Task ProcessParticipants(
        Guid userId, List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors, CancellationToken ct)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.Participants
                .FirstOrDefaultAsync(p => p.Id == item.Id && p.UserId == userId, ct);

            if (item.Op == "delete")
            {
                if (existing != null) existing.DeletedAt = DateTimeOffset.UtcNow;
                results.Add(new SyncItemResult(item.Id, "applied", DateTimeOffset.UtcNow, null));
                continue;
            }

            if (existing != null && existing.UpdatedAt > item.UpdatedAt)
            {
                results.Add(new SyncItemResult(item.Id, "skipped", existing.UpdatedAt, null));
                continue;
            }

            if (item.Data == null) { errors.Add($"participants/{item.Id}: missing data"); continue; }
            var data = item.Data.Value;

            var name = data.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
            var note = data.TryGetProperty("note", out var ntp) ? ntp.GetString() : null;

            var nameTaken = await _db.Participants.AnyAsync(p =>
                p.Id != item.Id && p.UserId == userId && p.Name == name && p.DeletedAt == null, ct);
            if (nameTaken) { errors.Add($"participants/{item.Id}: PARTICIPANT_NAME_TAKEN"); continue; }

            if (existing == null)
            {
                _db.Participants.Add(new Participant
                {
                    Id = item.Id, UserId = userId,
                    Name = name, Note = note, IsDefault = false
                });
            }
            else
            {
                if (!existing.IsDefault) { existing.Name = name; existing.Note = note; }
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }

    private async Task ProcessWallets(
        Guid userId, List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors, CancellationToken ct)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.Wallets
                .FirstOrDefaultAsync(w => w.Id == item.Id && w.UserId == userId, ct);

            if (item.Op == "delete")
            {
                if (existing != null) existing.DeletedAt = DateTimeOffset.UtcNow;
                results.Add(new SyncItemResult(item.Id, "applied", DateTimeOffset.UtcNow, null));
                continue;
            }

            if (existing != null && existing.UpdatedAt > item.UpdatedAt)
            {
                results.Add(new SyncItemResult(item.Id, "skipped", existing.UpdatedAt, null));
                continue;
            }

            if (item.Data == null) { errors.Add($"wallets/{item.Id}: missing data"); continue; }
            var data = item.Data.Value;

            var name = data.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
            var typeStr = data.TryGetProperty("type", out var tp) ? tp.GetString() : "Regular";
            var type = Enum.TryParse<WalletType>(typeStr, true, out var wt) ? wt : WalletType.Regular;
            decimal? creditLimit = data.TryGetProperty("creditLimit", out var clp) && clp.ValueKind != JsonValueKind.Null
                ? clp.GetDecimal() : null;

            if (type == WalletType.Credit && (creditLimit == null || creditLimit <= 0))
            {
                errors.Add($"wallets/{item.Id}: INVALID_CREDIT_LIMIT");
                continue;
            }

            if (existing == null)
            {
                _db.Wallets.Add(new Wallet
                {
                    Id = item.Id, UserId = userId, Name = name, Type = type,
                    CreditLimit = type == WalletType.Credit ? creditLimit : null,
                    InitialBalance = data.TryGetProperty("initialBalance", out var ibp) ? ibp.GetDecimal() : 0m,
                    Currency = data.TryGetProperty("currency", out var cp) ? cp.GetString() ?? "VND" : "VND",
                    Icon = data.TryGetProperty("icon", out var ip) ? ip.GetString() : null,
                    Color = data.TryGetProperty("color", out var colp) ? colp.GetString() : null
                });
            }
            else
            {
                existing.Name = name;
                if (existing.Type == WalletType.Credit && creditLimit.HasValue)
                    existing.CreditLimit = creditLimit;
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }

    private async Task ProcessWalletCategories(
        Guid userId, List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors, CancellationToken ct)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.WalletCategories
                .FirstOrDefaultAsync(wc => wc.Id == item.Id && wc.UserId == userId, ct);

            if (item.Op == "delete")
            {
                if (existing != null) existing.DeletedAt = DateTimeOffset.UtcNow;
                results.Add(new SyncItemResult(item.Id, "applied", DateTimeOffset.UtcNow, null));
                continue;
            }

            if (existing != null && existing.UpdatedAt > item.UpdatedAt)
            {
                results.Add(new SyncItemResult(item.Id, "skipped", existing.UpdatedAt, null));
                continue;
            }

            if (item.Data == null) { errors.Add($"walletCategories/{item.Id}: missing data"); continue; }
            var data = item.Data.Value;

            var walletId   = data.TryGetProperty("walletId", out var wp)   ? wp.GetGuid()   : Guid.Empty;
            var categoryId = data.TryGetProperty("categoryId", out var cap) ? cap.GetGuid() : Guid.Empty;

            if (existing == null)
            {
                _db.WalletCategories.Add(new WalletCategory
                {
                    Id = item.Id, UserId = userId,
                    WalletId = walletId, CategoryId = categoryId
                });
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }

    private async Task ProcessCategories(
        Guid userId, List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors, CancellationToken ct)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var isSystemRow = await _db.Categories.AnyAsync(c => c.Id == item.Id && c.UserId == null, ct);
            if (isSystemRow)
            {
                results.Add(new SyncItemResult(item.Id, "rejected", null, ErrorCodes.SystemCategoryReadOnly));
                continue;
            }

            var existing = await _db.Categories
                .ForUserOnly(userId)
                .FirstOrDefaultAsync(c => c.Id == item.Id, ct);

            if (item.Op == "delete")
            {
                if (existing != null) existing.DeletedAt = DateTimeOffset.UtcNow;
                results.Add(new SyncItemResult(item.Id, "applied", DateTimeOffset.UtcNow, null));
                continue;
            }

            if (existing != null && existing.UpdatedAt > item.UpdatedAt)
            {
                results.Add(new SyncItemResult(item.Id, "skipped", existing.UpdatedAt, null));
                continue;
            }

            if (item.Data == null) { errors.Add($"categories/{item.Id}: missing data"); continue; }
            var data = item.Data.Value;

            var name     = data.TryGetProperty("name", out var np)     ? np.GetString() ?? ""         : "";
            var typeStr  = data.TryGetProperty("type", out var tp)     ? tp.GetString()               : "Expense";
            var type     = Enum.TryParse<CategoryType>(typeStr, true, out var ct2) ? ct2 : CategoryType.Expense;
            var parentId = data.TryGetProperty("parentId", out var pip) && pip.ValueKind != JsonValueKind.Null
                ? pip.GetGuid() : (Guid?)null;

            if (existing == null)
            {
                _db.Categories.Add(new Category
                {
                    Id = item.Id, UserId = userId, Name = name, Type = type,
                    ParentId = parentId,
                    AppliesToAllWallets = data.TryGetProperty("appliesToAllWallets", out var ap) && ap.GetBoolean(),
                    Icon  = data.TryGetProperty("icon", out var ip)    ? ip.GetString()    : null,
                    Color = data.TryGetProperty("color", out var colp) ? colp.GetString()  : null
                });
            }
            else
            {
                existing.Name = name; existing.Type = type; existing.ParentId = parentId;
                existing.AppliesToAllWallets = data.TryGetProperty("appliesToAllWallets", out var ap2) && ap2.GetBoolean();
                existing.Icon  = data.TryGetProperty("icon", out var ip2)    ? ip2.GetString()    : null;
                existing.Color = data.TryGetProperty("color", out var colp2) ? colp2.GetString()  : null;
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }

    private async Task ProcessTransactions(
        Guid userId, List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors, CancellationToken ct)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.Transactions
                .FirstOrDefaultAsync(t => t.Id == item.Id && t.UserId == userId, ct);

            if (item.Op == "delete")
            {
                if (existing != null) existing.DeletedAt = DateTimeOffset.UtcNow;
                results.Add(new SyncItemResult(item.Id, "applied", DateTimeOffset.UtcNow, null));
                continue;
            }

            if (existing != null && existing.UpdatedAt > item.UpdatedAt)
            {
                results.Add(new SyncItemResult(item.Id, "skipped", existing.UpdatedAt, null));
                continue;
            }

            if (item.Data == null) { errors.Add($"transactions/{item.Id}: missing data"); continue; }
            var data = item.Data.Value;

            var amount      = data.TryGetProperty("amount", out var amtp) ? amtp.GetDecimal() : 0m;
            var categoryId  = data.TryGetProperty("categoryId", out var cap) ? cap.GetGuid() : Guid.Empty;
            var walletId    = data.TryGetProperty("walletId", out var wp) ? wp.GetGuid() : Guid.Empty;
            var occurredAt  = data.TryGetProperty("occurredAt", out var ocp)
                ? ocp.GetDateTimeOffset() : DateTimeOffset.UtcNow;
            Guid? participantId = data.TryGetProperty("participantId", out var pip) && pip.ValueKind != JsonValueKind.Null
                ? pip.GetGuid() : null;

            var category = await _db.Categories
                .ForUserIncludingSystem(userId)
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.DeletedAt == null, ct);
            if (category == null) { errors.Add($"transactions/{item.Id}: category not found"); continue; }

            if (category.Type == CategoryType.Debt && participantId == null)
            {
                var def = await _db.Participants.FirstOrDefaultAsync(p =>
                    p.UserId == userId && p.IsDefault && p.DeletedAt == null, ct);
                if (def == null) { errors.Add($"transactions/{item.Id}: DEFAULT_PARTICIPANT_MISSING"); continue; }
                participantId = def.Id;
            }

            var note = data.TryGetProperty("note", out var notep) ? notep.GetString() : null;

            if (existing == null)
            {
                _db.Transactions.Add(new Transaction
                {
                    Id = item.Id, UserId = userId,
                    WalletId = walletId, CategoryId = categoryId,
                    ParticipantId = participantId, Amount = amount,
                    OccurredAt = occurredAt, Note = note
                });
            }
            else
            {
                existing.WalletId = walletId; existing.CategoryId = categoryId;
                existing.ParticipantId = participantId; existing.Amount = amount;
                existing.OccurredAt = occurredAt; existing.Note = note;
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }
}
