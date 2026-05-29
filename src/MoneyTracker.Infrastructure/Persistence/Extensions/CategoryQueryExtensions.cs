using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MoneyTracker.Infrastructure.Persistence.Extensions;

public static class CategoryQueryExtensions
{
    /// <summary>Trả về system categories (UserId IS NULL) + user categories. Dùng cho list/GET/sync pull/report.</summary>
    public static IQueryable<Category> ForUserIncludingSystem(this IQueryable<Category> q, Guid userId)
        => q.Where(c => c.UserId == userId || c.UserId == null);

    /// <summary>Chỉ user-owned categories. Dùng cho PUT/DELETE và parent validation khi create.</summary>
    public static IQueryable<Category> ForUserOnly(this IQueryable<Category> q, Guid userId)
        => q.Where(c => c.UserId == userId);
}
