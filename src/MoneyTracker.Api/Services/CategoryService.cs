using MoneyTracker.Api.Dtos.Categories;
using MoneyTracker.Api.Services.Exceptions;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Api.Services;

public class CategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoryResponse>> ListAsync(Guid userId, CancellationToken ct)
    {
        var list = await _db.Categories
            .ForUserIncludingSystem(userId)
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.Type).ThenBy(c => c.ParentId).ThenBy(c => c.Name)
            .ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<CategoryResponse> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var c = await _db.Categories
            .ForUserIncludingSystem(userId)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c == null) throw new NotFoundException(ErrorCodes.NotFound);
        return ToDto(c);
    }

    public async Task<CategoryResponse> CreateAsync(Guid userId, CreateCategoryRequest req, CancellationToken ct)
    {
        if (req.ParentId.HasValue)
        {
            var parent = await _db.Categories
                .ForUserIncludingSystem(userId)
                .FirstOrDefaultAsync(p => p.Id == req.ParentId.Value && p.DeletedAt == null, ct);
            if (parent == null) throw new ValidationException(ErrorCodes.ParentNotFound);
            if (parent.Type != req.Type) throw new ValidationException(ErrorCodes.ParentTypeMismatch);
        }

        var id = req.Id ?? Guid.NewGuid();
        if (await _db.Categories.AnyAsync(c => c.Id == id, ct))
            throw new ConflictException(ErrorCodes.IdAlreadyExists);

        var category = new Category
        {
            Id = id,
            UserId = userId,
            Name = req.Name.Trim(),
            Type = req.Type,
            ParentId = req.ParentId,
            AppliesToAllWallets = req.AppliesToAllWallets,
            Icon = req.Icon,
            Color = req.Color
        };
        _db.Categories.Add(category);

        if (!req.AppliesToAllWallets && req.AssignToWalletIds is { Count: > 0 })
        {
            var validWalletIds = await _db.Wallets
                .Where(w => w.UserId == userId && w.DeletedAt == null && req.AssignToWalletIds.Contains(w.Id))
                .Select(w => w.Id)
                .ToListAsync(ct);

            foreach (var wid in validWalletIds)
            {
                _db.WalletCategories.Add(new WalletCategory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WalletId = wid,
                    CategoryId = id
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid userId, Guid id, UpdateCategoryRequest req, CancellationToken ct)
    {
        var c = await _db.Categories
            .ForUserOnly(userId)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c == null)
        {
            var isSystem = await _db.Categories.AnyAsync(x => x.Id == id && x.UserId == null, ct);
            if (isSystem) throw new ForbiddenException(ErrorCodes.SystemCategoryReadOnly);
            throw new NotFoundException(ErrorCodes.NotFound);
        }

        if (req.ParentId.HasValue)
        {
            if (req.ParentId.Value == id)
                throw new ValidationException(ErrorCodes.CannotBeOwnParent);
            var parent = await _db.Categories
                .ForUserIncludingSystem(userId)
                .FirstOrDefaultAsync(p => p.Id == req.ParentId.Value && p.DeletedAt == null, ct);
            if (parent == null) throw new ValidationException(ErrorCodes.ParentNotFound);
            if (parent.Type != c.Type) throw new ValidationException(ErrorCodes.ParentTypeMismatch);
        }

        c.Name = req.Name.Trim();
        c.ParentId = req.ParentId;
        c.AppliesToAllWallets = req.AppliesToAllWallets;
        c.Icon = req.Icon;
        c.Color = req.Color;

        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var c = await _db.Categories
            .ForUserOnly(userId)
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c == null)
        {
            var isSystem = await _db.Categories.AnyAsync(x => x.Id == id && x.UserId == null, ct);
            if (isSystem) throw new ForbiddenException(ErrorCodes.SystemCategoryReadOnly);
            throw new NotFoundException(ErrorCodes.NotFound);
        }

        if (c.Children.Any(ch => ch.DeletedAt == null))
            throw new ValidationException(ErrorCodes.HasChildren);

        if (await _db.Transactions.AnyAsync(t => t.CategoryId == id && t.DeletedAt == null, ct))
            throw new ValidationException(ErrorCodes.HasTransactions);

        c.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Guid>> GetAssignedWalletsAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var category = await _db.Categories
            .ForUserIncludingSystem(userId)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);
        if (category == null) throw new NotFoundException(ErrorCodes.NotFound);

        if (category.AppliesToAllWallets)
        {
            return await _db.Wallets
                .Where(w => w.UserId == userId && w.DeletedAt == null)
                .Select(w => w.Id)
                .ToListAsync(ct);
        }

        return await _db.WalletCategories
            .Where(wc => wc.CategoryId == id && wc.UserId == userId && wc.DeletedAt == null)
            .Select(wc => wc.WalletId)
            .ToListAsync(ct);
    }

    public async Task SetAssignedWalletsAsync(Guid userId, Guid id, List<Guid> walletIds, CancellationToken ct)
    {
        var category = await _db.Categories
            .ForUserOnly(userId)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);
        if (category == null)
        {
            var isSystem = await _db.Categories.AnyAsync(x => x.Id == id && x.UserId == null, ct);
            if (isSystem) throw new ForbiddenException(ErrorCodes.SystemCategoryReadOnly);
            throw new NotFoundException(ErrorCodes.NotFound);
        }

        if (category.AppliesToAllWallets)
            throw new ValidationException(ErrorCodes.CategoryAppliesToAll);

        var currentAssignments = await _db.WalletCategories
            .Where(wc => wc.CategoryId == id && wc.UserId == userId && wc.DeletedAt == null)
            .ToListAsync(ct);

        var desired = walletIds.Distinct().ToHashSet();
        var existing = currentAssignments.Select(wc => wc.WalletId).ToHashSet();

        var now = DateTimeOffset.UtcNow;
        foreach (var wc in currentAssignments.Where(wc => !desired.Contains(wc.WalletId)))
            wc.DeletedAt = now;

        var toAdd = desired.Except(existing).ToList();
        if (toAdd.Count > 0)
        {
            var validWalletIds = await _db.Wallets
                .Where(w => w.UserId == userId && w.DeletedAt == null && toAdd.Contains(w.Id))
                .Select(w => w.Id)
                .ToListAsync(ct);
            foreach (var wid in validWalletIds)
            {
                _db.WalletCategories.Add(new WalletCategory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WalletId = wid,
                    CategoryId = id
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public static CategoryResponse ToDto(Category c) => new(
        c.Id, c.Name, c.Type, c.ParentId, c.AppliesToAllWallets,
        c.Icon, c.Color,
        IsSystem: c.UserId == null,
        SystemKey: c.SystemKey,
        c.CreatedAt, c.UpdatedAt);
}
