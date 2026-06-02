using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<ParticipantLink> ParticipantLinks => Set<ParticipantLink>();
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();
    public DbSet<TransactionAudit> TransactionAudits => Set<TransactionAudit>();
    public DbSet<HouseholdWalletShare> HouseholdWalletShares => Set<HouseholdWalletShare>();
    public DbSet<HouseholdWalletShareTarget> HouseholdWalletShareTargets => Set<HouseholdWalletShareTarget>();

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

    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
    }
}
