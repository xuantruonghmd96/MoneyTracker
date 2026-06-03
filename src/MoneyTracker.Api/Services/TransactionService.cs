using MoneyTracker.Api.Dtos.Transactions;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Services;

public class TransactionService
{
    private readonly AppDbContext _db;

    public TransactionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TransactionResponse>> ListAsync(
        Guid userId,
        DateTimeOffset from, DateTimeOffset to,
        Guid? walletId, Guid? categoryId, Guid? participantId,
        CancellationToken ct)
    {
        var q = _db.Transactions
            .Where(t => t.UserId == userId && t.DeletedAt == null
                     && t.OccurredAt >= from && t.OccurredAt <= to);

        if (walletId.HasValue)     q = q.Where(t => t.WalletId == walletId.Value);
        if (categoryId.HasValue)   q = q.Where(t => t.CategoryId == categoryId.Value);
        if (participantId.HasValue) q = q.Where(t => t.ParticipantId == participantId.Value);

        return await q
            .OrderByDescending(t => t.OccurredAt)
            .Select(t => ToDto(t))
            .ToListAsync(ct);
    }

    public async Task<TransactionResponse> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var t = await _db.Transactions
            .Where(x => x.Id == id && x.UserId == userId && x.DeletedAt == null)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(ct);
        if (t == null) throw new NotFoundException(ErrorCodes.NotFound);
        return t;
    }

    public async Task<TransactionResponse> CreateAsync(Guid userId, CreateTransactionRequest req, CancellationToken ct)
    {
        var (category, resolvedParticipantId) = await ValidateAsync(
            userId, req.WalletId!.Value, req.CategoryId!.Value, req.ParticipantId, ct);

        var id = req.Id ?? Guid.NewGuid();
        if (await _db.Transactions.AnyAsync(t => t.Id == id, ct))
            throw new ConflictException(ErrorCodes.IdAlreadyExists);

        var tx = new Transaction
        {
            Id = id,
            UserId = userId,
            WalletId = req.WalletId!.Value,
            CategoryId = req.CategoryId!.Value,
            ParticipantId = resolvedParticipantId,
            Amount = req.Amount!.Value,
            OccurredAt = req.OccurredAt!.Value,
            Note = req.Note
        };
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return await GetSingleDtoAsync(tx.Id, ct);
    }

    public async Task<TransactionResponse> UpdateAsync(Guid userId, Guid id, UpdateTransactionRequest req, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId && x.DeletedAt == null, ct);
        if (tx == null) throw new NotFoundException(ErrorCodes.NotFound);

        var (_, resolvedParticipantId) = await ValidateAsync(
            userId, req.WalletId!.Value, req.CategoryId!.Value, req.ParticipantId, ct);

        tx.WalletId = req.WalletId!.Value;
        tx.CategoryId = req.CategoryId!.Value;
        tx.ParticipantId = resolvedParticipantId;
        tx.Amount = req.Amount!.Value;
        tx.OccurredAt = req.OccurredAt!.Value;
        tx.Note = req.Note;

        await _db.SaveChangesAsync(ct);
        return await GetSingleDtoAsync(tx.Id, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId && x.DeletedAt == null, ct);
        if (tx == null) throw new NotFoundException(ErrorCodes.NotFound);

        tx.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<(Category category, Guid? resolvedParticipantId)> ValidateAsync(
        Guid userId, Guid walletId, Guid categoryId, Guid? participantId, CancellationToken ct)
    {
        var walletExists = await _db.Wallets
            .AnyAsync(w => w.Id == walletId && w.UserId == userId && w.DeletedAt == null, ct);
        if (!walletExists) throw new NotFoundException(ErrorCodes.NotFound);

        var category = await _db.Categories
            .ForUserIncludingSystem(userId)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.DeletedAt == null, ct);
        if (category == null) throw new NotFoundException(ErrorCodes.NotFound);

        if (!category.AppliesToAllWallets)
        {
            var assigned = await _db.WalletCategories.AnyAsync(wc =>
                wc.WalletId == walletId && wc.CategoryId == categoryId
                && wc.UserId == userId && wc.DeletedAt == null, ct);
            if (!assigned) throw new ValidationException(ErrorCodes.CategoryNotAssignedToWallet);
        }

        Guid? resolvedParticipantId = participantId;
        if (category.Type == CategoryType.Debt)
        {
            if (participantId == null)
            {
                var def = await _db.Participants.FirstOrDefaultAsync(p =>
                    p.UserId == userId && p.IsDefault && p.DeletedAt == null, ct);
                if (def == null) throw new DomainException(ErrorCodes.DefaultParticipantMissing);
                resolvedParticipantId = def.Id;
            }
            else
            {
                var pExists = await _db.Participants.AnyAsync(p =>
                    p.Id == participantId && p.UserId == userId && p.DeletedAt == null, ct);
                if (!pExists) throw new NotFoundException(ErrorCodes.NotFound);
            }
        }
        else if (participantId.HasValue)
        {
            var pExists = await _db.Participants.AnyAsync(p =>
                p.Id == participantId && p.UserId == userId && p.DeletedAt == null, ct);
            if (!pExists) throw new NotFoundException(ErrorCodes.NotFound);
        }

        return (category, resolvedParticipantId);
    }

    private async Task<TransactionResponse> GetSingleDtoAsync(Guid id, CancellationToken ct)
    {
        return await _db.Transactions
            .Where(x => x.Id == id)
            .Select(x => ToDto(x))
            .FirstAsync(ct);
    }

    private static TransactionResponse ToDto(Transaction t) => new(
        t.Id, t.Amount, t.OccurredAt, t.CategoryId, t.WalletId, t.ParticipantId,
        t.Note, t.CreatedAt, t.UpdatedAt, t.DeletedAt);
}
