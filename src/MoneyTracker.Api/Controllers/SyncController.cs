using System.Text.Json;
using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Common;
using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Api.Dtos.Participants;
using MoneyTracker.Api.Dtos.Sync;
using MoneyTracker.Api.Dtos.Transactions;
using MoneyTracker.Api.Dtos.Wallets;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SyncController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpPost("push")]
    public async Task<ActionResult<SyncPushResponse>> Push([FromBody] SyncPushRequest req)
    {
        // Idempotency: nếu batch đã xử lý, trả lại cached response
        var existing = await _db.SyncBatches
            .FirstOrDefaultAsync(b => b.Id == req.BatchId && b.UserId == _currentUser.Id);
        if (existing != null)
            return Ok(JsonSerializer.Deserialize<SyncPushResponse>(existing.ResponseJson, _jsonOpts));

        var results = new SyncPushResults(
            new List<SyncItemResult>(), new List<SyncItemResult>(),
            new List<SyncItemResult>(), new List<SyncItemResult>(),
            new List<SyncItemResult>());

        await using var dbTx = await _db.Database.BeginTransactionAsync();

        var errors = new List<string>();

        // Process in order: participants → wallets → walletCategories → categories → transactions
        await ProcessParticipants(req.Changes.Participants, results.Participants!, errors);
        await ProcessWallets(req.Changes.Wallets, results.Wallets!, errors);
        await ProcessWalletCategories(req.Changes.WalletCategories, results.WalletCategories!, errors);
        await ProcessCategories(req.Changes.Categories, results.Categories!, errors);
        await ProcessTransactions(req.Changes.Transactions, results.Transactions!, errors);

        if (errors.Count > 0)
        {
            await dbTx.RollbackAsync();
            return BadRequest(new ApiError(ErrorCodes.SyncBatchRejected,
                errors.Select((e, i) => (Key: i.ToString(), Value: e))
                      .ToDictionary(x => x.Key, x => x.Value)));
        }

        var serverNow = DateTimeOffset.UtcNow;
        var response = new SyncPushResponse(results, serverNow);
        var responseJson = JsonSerializer.Serialize(response, _jsonOpts);

        _db.SyncBatches.Add(new SyncBatch
        {
            Id = req.BatchId,
            UserId = _currentUser.Id,
            ProcessedAt = serverNow,
            ResponseJson = responseJson
        });

        await _db.SaveChangesAsync();
        await dbTx.CommitAsync();

        return Ok(response);
    }

    [HttpGet("pull")]
    public async Task<ActionResult<SyncPullResponse>> Pull([FromQuery] DateTimeOffset? since)
    {
        var uid = _currentUser.Id;
        var serverNow = DateTimeOffset.UtcNow;

        var wallets = await _db.Wallets
            .Where(w => w.UserId == uid && (since == null || w.UpdatedAt > since))
            .Select(w => new WalletResponse(
                w.Id, w.Name, w.Type, w.CreditLimit, w.InitialBalance,
                w.Currency, w.Icon, w.Color, w.CreatedAt, w.UpdatedAt))
            .ToListAsync();

        var categories = await _db.Categories
            .ForUserIncludingSystem(uid)
            .Where(c => since == null || c.UpdatedAt > since)
            .Select(c => new CategoryResponse(
                c.Id, c.Name, c.Type, c.ParentId, c.AppliesToAllWallets,
                c.Icon, c.Color, c.UserId == null, c.SystemKey,
                c.CreatedAt, c.UpdatedAt))
            .ToListAsync();

        var walletCategories = await _db.WalletCategories
            .Where(wc => wc.UserId == uid && (since == null || wc.UpdatedAt > since))
            .Select(wc => new WalletCategoryAssignmentResponse(wc.Id, wc.WalletId, wc.CategoryId, wc.CreatedAt))
            .ToListAsync();

        var participants = await _db.Participants
            .Where(p => p.UserId == uid && (since == null || p.UpdatedAt > since))
            .Select(p => new ParticipantResponse(p.Id, p.Name, p.Note, p.IsDefault, p.CreatedAt, p.UpdatedAt))
            .ToListAsync();

        var transactions = await _db.Transactions
            .Where(t => t.UserId == uid && (since == null || t.UpdatedAt > since))
            .Select(t => new TransactionResponse(
                t.Id, t.Amount, t.OccurredAt, t.WalletId, t.Note,
                new CategoryRef(
                    t.Category!.Id, t.Category.Name, t.Category.Type,
                    t.Category.UserId == null, t.Category.SystemKey,
                    t.Category.Icon, t.Category.Color),
                t.Participant == null ? null : new ParticipantRef(
                    t.Participant.Id, t.Participant.Name, t.Participant.IsDefault),
                t.CreatedAt, t.UpdatedAt, t.DeletedAt))
            .ToListAsync();

        return Ok(new SyncPullResponse(wallets, categories, walletCategories, participants, transactions, serverNow));
    }

    // ===== Push helpers =====

    private async Task ProcessParticipants(
        List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.Participants
                .FirstOrDefaultAsync(p => p.Id == item.Id && p.UserId == _currentUser.Id);

            if (item.Op == "delete")
            {
                if (existing != null) existing.DeletedAt = DateTimeOffset.UtcNow;
                results.Add(new SyncItemResult(item.Id, "applied", DateTimeOffset.UtcNow, null));
                continue;
            }

            // LWW
            if (existing != null && existing.UpdatedAt > item.UpdatedAt)
            {
                results.Add(new SyncItemResult(item.Id, "skipped", existing.UpdatedAt, null));
                continue;
            }

            if (item.Data == null) { errors.Add($"participants/{item.Id}: missing data"); continue; }
            var data = item.Data.Value;

            var name = data.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
            var note = data.TryGetProperty("note", out var ntp) ? ntp.GetString() : null;

            // Validate name uniqueness (exclude self)
            var nameTaken = await _db.Participants.AnyAsync(p =>
                p.Id != item.Id && p.UserId == _currentUser.Id && p.Name == name && p.DeletedAt == null);
            if (nameTaken) { errors.Add($"participants/{item.Id}: PARTICIPANT_NAME_TAKEN"); continue; }

            if (existing == null)
            {
                _db.Participants.Add(new Participant
                {
                    Id = item.Id, UserId = _currentUser.Id,
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
        List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.Wallets
                .FirstOrDefaultAsync(w => w.Id == item.Id && w.UserId == _currentUser.Id);

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
                    Id = item.Id, UserId = _currentUser.Id, Name = name, Type = type,
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
        List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.WalletCategories
                .FirstOrDefaultAsync(wc => wc.Id == item.Id && wc.UserId == _currentUser.Id);

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
                    Id = item.Id, UserId = _currentUser.Id,
                    WalletId = walletId, CategoryId = categoryId
                });
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }

    private async Task ProcessCategories(
        List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            // System categories: reject silently (client should not push system categories)
            var isSystemRow = await _db.Categories.AnyAsync(c => c.Id == item.Id && c.UserId == null);
            if (isSystemRow)
            {
                results.Add(new SyncItemResult(item.Id, "rejected", null, ErrorCodes.SystemCategoryReadOnly));
                continue;
            }

            var existing = await _db.Categories
                .ForUserOnly(_currentUser.Id)
                .FirstOrDefaultAsync(c => c.Id == item.Id);

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
            var type     = Enum.TryParse<CategoryType>(typeStr, true, out var ct) ? ct : CategoryType.Expense;
            var parentId = data.TryGetProperty("parentId", out var pip) && pip.ValueKind != JsonValueKind.Null
                ? pip.GetGuid() : (Guid?)null;

            if (existing == null)
            {
                _db.Categories.Add(new Category
                {
                    Id = item.Id, UserId = _currentUser.Id, Name = name, Type = type,
                    ParentId = parentId,
                    AppliesToAllWallets = data.TryGetProperty("appliesToAllWallets", out var ap) && ap.GetBoolean(),
                    Icon  = data.TryGetProperty("icon", out var ip)   ? ip.GetString()   : null,
                    Color = data.TryGetProperty("color", out var colp) ? colp.GetString() : null
                });
            }
            else
            {
                existing.Name = name; existing.Type = type; existing.ParentId = parentId;
                existing.AppliesToAllWallets = data.TryGetProperty("appliesToAllWallets", out var ap2) && ap2.GetBoolean();
                existing.Icon  = data.TryGetProperty("icon", out var ip2)   ? ip2.GetString()   : null;
                existing.Color = data.TryGetProperty("color", out var colp2) ? colp2.GetString() : null;
            }
            results.Add(new SyncItemResult(item.Id, "applied", null, null));
        }
    }

    private async Task ProcessTransactions(
        List<SyncChangeItem>? items, List<SyncItemResult> results, List<string> errors)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            var existing = await _db.Transactions
                .FirstOrDefaultAsync(t => t.Id == item.Id && t.UserId == _currentUser.Id);

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

            // Validate category
            var category = await _db.Categories
                .ForUserIncludingSystem(_currentUser.Id)
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.DeletedAt == null);
            if (category == null) { errors.Add($"transactions/{item.Id}: category not found"); continue; }

            // Debt participant auto-fallback
            if (category.Type == CategoryType.Debt && participantId == null)
            {
                var def = await _db.Participants.FirstOrDefaultAsync(p =>
                    p.UserId == _currentUser.Id && p.IsDefault && p.DeletedAt == null);
                if (def == null) { errors.Add($"transactions/{item.Id}: DEFAULT_PARTICIPANT_MISSING"); continue; }
                participantId = def.Id;
            }

            var note = data.TryGetProperty("note", out var notep) ? notep.GetString() : null;

            if (existing == null)
            {
                _db.Transactions.Add(new Transaction
                {
                    Id = item.Id, UserId = _currentUser.Id,
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
