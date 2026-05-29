using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Common;
using MoneyTracker.Api.Dtos.Transactions;
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
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TransactionsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<TransactionResponse>>> List(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? walletId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? participantId)
    {
        var q = _db.Transactions
            .Where(t => t.UserId == _currentUser.Id && t.DeletedAt == null
                     && t.OccurredAt >= from && t.OccurredAt <= to);

        if (walletId.HasValue)    q = q.Where(t => t.WalletId == walletId.Value);
        if (categoryId.HasValue)  q = q.Where(t => t.CategoryId == categoryId.Value);
        if (participantId.HasValue) q = q.Where(t => t.ParticipantId == participantId.Value);

        var list = await q
            .OrderByDescending(t => t.OccurredAt)
            .Select(t => new TransactionResponse(
                t.Id,
                t.Amount,
                t.OccurredAt,
                t.WalletId,
                t.Note,
                new CategoryRef(
                    t.Category!.Id, t.Category.Name, t.Category.Type,
                    t.Category.UserId == null, t.Category.SystemKey,
                    t.Category.Icon, t.Category.Color),
                t.Participant == null ? null : new ParticipantRef(
                    t.Participant.Id, t.Participant.Name, t.Participant.IsDefault),
                t.CreatedAt, t.UpdatedAt, t.DeletedAt))
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Get(Guid id)
    {
        var t = await _db.Transactions
            .Where(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null)
            .Select(x => new TransactionResponse(
                x.Id, x.Amount, x.OccurredAt, x.WalletId, x.Note,
                new CategoryRef(
                    x.Category!.Id, x.Category.Name, x.Category.Type,
                    x.Category.UserId == null, x.Category.SystemKey,
                    x.Category.Icon, x.Category.Color),
                x.Participant == null ? null : new ParticipantRef(
                    x.Participant.Id, x.Participant.Name, x.Participant.IsDefault),
                x.CreatedAt, x.UpdatedAt, x.DeletedAt))
            .FirstOrDefaultAsync();

        return t == null ? NotFound(new ApiError(ErrorCodes.NotFound)) : Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] CreateTransactionRequest req)
    {
        var (validation, walletOk, category, participantId) = await ValidateTransaction(
            req.WalletId!.Value, req.CategoryId!.Value, req.ParticipantId, req.Amount!.Value);
        if (validation != null) return validation;

        var id = req.Id ?? Guid.NewGuid();
        if (await _db.Transactions.AnyAsync(t => t.Id == id))
            return Conflict(new ApiError(ErrorCodes.IdAlreadyExists));

        var tx = new Transaction
        {
            Id = id,
            UserId = _currentUser.Id,
            WalletId = req.WalletId!.Value,
            CategoryId = req.CategoryId!.Value,
            ParticipantId = participantId,
            Amount = req.Amount!.Value,
            OccurredAt = req.OccurredAt!.Value,
            Note = req.Note
        };
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = tx.Id }, await GetSingleDto(tx.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(Guid id, [FromBody] UpdateTransactionRequest req)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (tx == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        var (validation, _, _, participantId) = await ValidateTransaction(
            req.WalletId!.Value, req.CategoryId!.Value, req.ParticipantId, req.Amount!.Value);
        if (validation != null) return validation;

        tx.WalletId = req.WalletId!.Value;
        tx.CategoryId = req.CategoryId!.Value;
        tx.ParticipantId = participantId;
        tx.Amount = req.Amount!.Value;
        tx.OccurredAt = req.OccurredAt!.Value;
        tx.Note = req.Note;

        await _db.SaveChangesAsync();
        return Ok(await GetSingleDto(tx.Id));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (tx == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        tx.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Returns (errorResult, walletOk, category, resolvedParticipantId)
    private async Task<(ActionResult?, bool, Category?, Guid?)> ValidateTransaction(
        Guid walletId, Guid categoryId, Guid? participantId, decimal amount)
    {
        // Wallet phải thuộc user
        var walletExists = await _db.Wallets
            .AnyAsync(w => w.Id == walletId && w.UserId == _currentUser.Id && w.DeletedAt == null);
        if (!walletExists)
            return (NotFound(new ApiError(ErrorCodes.NotFound)), false, null, null);

        // Category: system + user
        var category = await _db.Categories
            .ForUserIncludingSystem(_currentUser.Id)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.DeletedAt == null);
        if (category == null)
            return (NotFound(new ApiError(ErrorCodes.NotFound)), true, null, null);

        // Nếu category không AppliesToAllWallets → phải có WalletCategory row
        if (!category.AppliesToAllWallets)
        {
            var assigned = await _db.WalletCategories.AnyAsync(wc =>
                wc.WalletId == walletId && wc.CategoryId == categoryId
                && wc.UserId == _currentUser.Id && wc.DeletedAt == null);
            if (!assigned)
                return (BadRequest(new ApiError(ErrorCodes.CategoryNotAssignedToWallet)), true, category, null);
        }

        // Resolve participant
        Guid? resolvedParticipantId = participantId;
        if (category.Type == CategoryType.Debt)
        {
            if (participantId == null)
            {
                var def = await _db.Participants.FirstOrDefaultAsync(p =>
                    p.UserId == _currentUser.Id && p.IsDefault && p.DeletedAt == null);
                if (def == null)
                    return (StatusCode(500, new ApiError(ErrorCodes.DefaultParticipantMissing)), true, category, null);
                resolvedParticipantId = def.Id;
            }
            else
            {
                var pExists = await _db.Participants.AnyAsync(p =>
                    p.Id == participantId && p.UserId == _currentUser.Id && p.DeletedAt == null);
                if (!pExists)
                    return (NotFound(new ApiError(ErrorCodes.NotFound)), true, category, null);
            }
        }
        else if (participantId.HasValue)
        {
            var pExists = await _db.Participants.AnyAsync(p =>
                p.Id == participantId && p.UserId == _currentUser.Id && p.DeletedAt == null);
            if (!pExists)
                return (NotFound(new ApiError(ErrorCodes.NotFound)), true, category, null);
        }

        return (null, true, category, resolvedParticipantId);
    }

    private async Task<TransactionResponse> GetSingleDto(Guid id)
    {
        return await _db.Transactions
            .Where(x => x.Id == id)
            .Select(x => new TransactionResponse(
                x.Id, x.Amount, x.OccurredAt, x.WalletId, x.Note,
                new CategoryRef(
                    x.Category!.Id, x.Category.Name, x.Category.Type,
                    x.Category.UserId == null, x.Category.SystemKey,
                    x.Category.Icon, x.Category.Color),
                x.Participant == null ? null : new ParticipantRef(
                    x.Participant.Id, x.Participant.Name, x.Participant.IsDefault),
                x.CreatedAt, x.UpdatedAt, x.DeletedAt))
            .FirstAsync();
    }
}
