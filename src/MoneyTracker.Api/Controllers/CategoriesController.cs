using MoneyTracker.Api.Auth;
using MoneyTracker.Api.Common;
using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> List()
    {
        var list = await _db.Categories
            .Where(c => c.UserId == _currentUser.Id && c.DeletedAt == null)
            .OrderBy(c => c.Type).ThenBy(c => c.ParentId).ThenBy(c => c.Name)
            .ToListAsync();
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Get(Guid id)
    {
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        return c == null ? NotFound(new ApiError(ErrorCodes.NotFound)) : Ok(ToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CreateCategoryRequest req)
    {
        if (req.ParentId.HasValue)
        {
            var parent = await _db.Categories.FirstOrDefaultAsync(p =>
                p.Id == req.ParentId.Value && p.UserId == _currentUser.Id && p.DeletedAt == null);
            if (parent == null) return BadRequest(new ApiError(ErrorCodes.ParentNotFound));
            if (parent.Type != req.Type)
                return BadRequest(new ApiError(ErrorCodes.ParentTypeMismatch));
        }

        var id = req.Id ?? Guid.NewGuid();
        if (await _db.Categories.AnyAsync(c => c.Id == id))
            return Conflict(new ApiError(ErrorCodes.IdAlreadyExists));

        var category = new Category
        {
            Id = id,
            UserId = _currentUser.Id,
            Name = req.Name.Trim(),
            Type = req.Type,
            ParentId = req.ParentId,
            AppliesToAllWallets = req.AppliesToAllWallets,
            Icon = req.Icon,
            Color = req.Color
        };
        _db.Categories.Add(category);

        // Optional: assign to specific wallets right away
        if (!req.AppliesToAllWallets && req.AssignToWalletIds is { Count: > 0 })
        {
            var validWalletIds = await _db.Wallets
                .Where(w => w.UserId == _currentUser.Id && w.DeletedAt == null && req.AssignToWalletIds.Contains(w.Id))
                .Select(w => w.Id)
                .ToListAsync();

            foreach (var wid in validWalletIds)
            {
                _db.WalletCategories.Add(new WalletCategory
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUser.Id,
                    WalletId = wid,
                    CategoryId = id
                });
            }
        }

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = category.Id }, ToDto(category));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(Guid id, [FromBody] UpdateCategoryRequest req)
    {
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (c == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        if (req.ParentId.HasValue)
        {
            if (req.ParentId.Value == id)
                return BadRequest(new ApiError(ErrorCodes.CannotBeOwnParent));
            var parent = await _db.Categories.FirstOrDefaultAsync(p =>
                p.Id == req.ParentId.Value && p.UserId == _currentUser.Id && p.DeletedAt == null);
            if (parent == null) return BadRequest(new ApiError(ErrorCodes.ParentNotFound));
            if (parent.Type != c.Type) return BadRequest(new ApiError(ErrorCodes.ParentTypeMismatch));
            // TODO: cycle check khi cây sâu hơn 1 cấp
        }

        c.Name = req.Name.Trim();
        c.ParentId = req.ParentId;
        c.AppliesToAllWallets = req.AppliesToAllWallets;
        c.Icon = req.Icon;
        c.Color = req.Color;

        await _db.SaveChangesAsync();
        return Ok(ToDto(c));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var c = await _db.Categories
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == _currentUser.Id && x.DeletedAt == null);
        if (c == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        if (c.Children.Any(ch => ch.DeletedAt == null))
            return BadRequest(new ApiError(ErrorCodes.HasChildren));

        var hasTx = await _db.Transactions.AnyAsync(t => t.CategoryId == id && t.DeletedAt == null);
        if (hasTx)
            return BadRequest(new ApiError(ErrorCodes.HasTransactions));

        c.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ===== Wallet-Category assignment =====

    [HttpGet("{id:guid}/wallets")]
    public async Task<ActionResult<List<Guid>>> GetAssignedWallets(Guid id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == _currentUser.Id && c.DeletedAt == null);
        if (category == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        if (category.AppliesToAllWallets)
        {
            var allIds = await _db.Wallets
                .Where(w => w.UserId == _currentUser.Id && w.DeletedAt == null)
                .Select(w => w.Id)
                .ToListAsync();
            return Ok(allIds);
        }

        var ids = await _db.WalletCategories
            .Where(wc => wc.CategoryId == id && wc.UserId == _currentUser.Id && wc.DeletedAt == null)
            .Select(wc => wc.WalletId)
            .ToListAsync();
        return Ok(ids);
    }

    /// <summary>Replace toàn bộ list ví được assign cho category (chỉ khi AppliesToAllWallets = false).</summary>
    [HttpPut("{id:guid}/wallets")]
    public async Task<IActionResult> SetAssignedWallets(Guid id, [FromBody] List<Guid> walletIds)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == _currentUser.Id && c.DeletedAt == null);
        if (category == null) return NotFound(new ApiError(ErrorCodes.NotFound));

        if (category.AppliesToAllWallets)
            return BadRequest(new ApiError(ErrorCodes.CategoryAppliesToAll));

        var currentAssignments = await _db.WalletCategories
            .Where(wc => wc.CategoryId == id && wc.UserId == _currentUser.Id && wc.DeletedAt == null)
            .ToListAsync();

        var desired = walletIds.Distinct().ToHashSet();
        var existing = currentAssignments.Select(wc => wc.WalletId).ToHashSet();

        // Soft-delete những row không còn cần
        var now = DateTimeOffset.UtcNow;
        foreach (var wc in currentAssignments.Where(wc => !desired.Contains(wc.WalletId)))
            wc.DeletedAt = now;

        // Validate và thêm mới
        var toAdd = desired.Except(existing).ToList();
        if (toAdd.Count > 0)
        {
            var validWalletIds = await _db.Wallets
                .Where(w => w.UserId == _currentUser.Id && w.DeletedAt == null && toAdd.Contains(w.Id))
                .Select(w => w.Id)
                .ToListAsync();
            foreach (var wid in validWalletIds)
            {
                _db.WalletCategories.Add(new WalletCategory
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUser.Id,
                    WalletId = wid,
                    CategoryId = id
                });
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static CategoryResponse ToDto(Category c) => new(
        c.Id, c.Name, c.Type, c.ParentId, c.AppliesToAllWallets,
        c.Icon, c.Color, c.CreatedAt, c.UpdatedAt);
}
