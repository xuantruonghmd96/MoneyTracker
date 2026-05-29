using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MoneyTracker.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<WalletCategory> WalletCategories => Set<WalletCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<HouseholdCategoryShare> HouseholdCategoryShares => Set<HouseholdCategoryShare>();
    public DbSet<HouseholdCategoryShareTarget> HouseholdCategoryShareTargets => Set<HouseholdCategoryShareTarget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tự động set CreatedAt/UpdatedAt cho mọi entity. ISyncEntity là điều kiện đủ;
    /// vài entity ngoài (User, Household, ...) cũng có 2 field này nhưng không qua interface
    /// nên xử lý theo reflection nhẹ.
    /// </summary>
    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                continue;

            var entity = entry.Entity;
            var type = entity.GetType();

            var createdAt = type.GetProperty("CreatedAt");
            var updatedAt = type.GetProperty("UpdatedAt");

            if (entry.State == EntityState.Added && createdAt != null && createdAt.CanWrite)
            {
                var current = (DateTimeOffset?)createdAt.GetValue(entity);
                if (current == null || current == default(DateTimeOffset))
                    createdAt.SetValue(entity, now);
            }

            if (updatedAt != null && updatedAt.CanWrite)
            {
                updatedAt.SetValue(entity, now);
            }
        }
    }
}
