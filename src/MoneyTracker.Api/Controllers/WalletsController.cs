using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Dtos.Wallets;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/wallets")]
public class WalletsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public WalletsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<WalletResponse>>> List()
    {
        var wallets = await _db.Wallets
            .Where(w => w.UserId == _currentUser.Id && w.DeletedAt == null)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();

        return Ok(wallets.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WalletResponse>> Get(Guid id)
    {
        var w = await _db.Wallets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        return w == null ? NotFound() : Ok(ToDto(w));
    }

    [HttpPost]
    public async Task<ActionResult<WalletResponse>> Create([FromBody] CreateWalletRequest req)
    {
        if (req.Type == WalletType.Credit && req.CreditLimit is null or <= 0)
            return BadRequest(new { error = "INVALID_CREDIT_LIMIT", message = "Ví tín dụng phải có hạn mức > 0." });

        var id = req.Id ?? Guid.NewGuid();
        if (await _db.Wallets.AnyAsync(w => w.Id == id))
            return Conflict(new { error = "ID_ALREADY_EXISTS" });

        var wallet = new Wallet
        {
            Id = id,
            UserId = _currentUser.Id,
            Name = req.Name.Trim(),
            Type = req.Type,
            CreditLimit = req.Type == WalletType.Credit ? req.CreditLimit : null,
            InitialBalance = req.InitialBalance ?? 0m,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "VND" : req.Currency!.ToUpperInvariant(),
            Icon = req.Icon,
            Color = req.Color
        };
        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = wallet.Id }, ToDto(wallet));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WalletResponse>> Update(Guid id, [FromBody] UpdateWalletRequest req)
    {
        var w = await _db.Wallets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (w == null) return NotFound();

        w.Name = req.Name.Trim();
        if (w.Type == WalletType.Credit)
        {
            if (req.CreditLimit is null or <= 0)
                return BadRequest(new { error = "INVALID_CREDIT_LIMIT" });
            w.CreditLimit = req.CreditLimit;
        }
        if (req.InitialBalance.HasValue) w.InitialBalance = req.InitialBalance.Value;
        w.Icon = req.Icon;
        w.Color = req.Color;

        await _db.SaveChangesAsync();
        return Ok(ToDto(w));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var w = await _db.Wallets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (w == null) return NotFound();

        // Soft delete (tombstone cho sync). Để giữ giao dịch lịch sử, không cascade delete transactions.
        w.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static WalletResponse ToDto(Wallet w) => new(
        w.Id, w.Name, w.Type, w.CreditLimit, w.InitialBalance,
        w.Currency, w.Icon, w.Color, w.CreatedAt, w.UpdatedAt);
}
