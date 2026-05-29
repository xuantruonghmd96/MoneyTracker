using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyTracker.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.ToTable("wallets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.CreditLimit).HasColumnType("numeric(18,2)");
        b.Property(x => x.InitialBalance).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        b.Property(x => x.Currency).IsRequired().HasMaxLength(8).HasDefaultValue("VND");
        b.Property(x => x.Icon).HasMaxLength(64);
        b.Property(x => x.Color).HasMaxLength(16);

        b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        b.HasIndex(x => new { x.UserId, x.DeletedAt });

        b.HasOne(x => x.User)
            .WithMany(u => u.Wallets)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(true);

        // Constraint: ví Credit phải có CreditLimit
        b.ToTable(t => t.HasCheckConstraint(
            "ck_wallets_credit_limit",
            "(type = 0) OR (type = 1 AND credit_limit IS NOT NULL)"));
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    // Hard-coded UUIDs cho system categories (stable across deployments)
    public static readonly Guid DebtLendId    = new("11111111-1111-1111-1111-111111111001");
    public static readonly Guid DebtCollectId = new("11111111-1111-1111-1111-111111111002");
    public static readonly Guid DebtBorrowId  = new("11111111-1111-1111-1111-111111111003");
    public static readonly Guid DebtRepayId   = new("11111111-1111-1111-1111-111111111004");

    private static readonly DateTimeOffset SystemCategorySeedTime =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.AppliesToAllWallets).HasDefaultValue(false);
        b.Property(x => x.Icon).HasMaxLength(64);
        b.Property(x => x.Color).HasMaxLength(16);
        b.Property(x => x.SystemKey).HasMaxLength(64);

        b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        b.HasIndex(x => new { x.UserId, x.ParentId });
        b.HasIndex(x => new { x.UserId, x.DeletedAt });

        // Partial unique index cho system_key
        b.HasIndex(x => x.SystemKey)
            .HasFilter("user_id IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_categories_system_key");

        // Constraint: system_key và user_id phải nhất quán
        b.ToTable(t => t.HasCheckConstraint(
            "ck_categories_system_consistency",
            "(user_id IS NULL AND system_key IS NOT NULL) OR (user_id IS NOT NULL AND system_key IS NULL)"));

        b.HasOne(x => x.User)
            .WithMany(u => u.Categories)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        b.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed 4 system categories (Debt)
        b.HasData(
            new Category
            {
                Id = DebtLendId, SystemKey = "DEBT_LEND", Name = "Cho vay",
                Type = CategoryType.Debt, AppliesToAllWallets = true,
                CreatedAt = SystemCategorySeedTime, UpdatedAt = SystemCategorySeedTime
            },
            new Category
            {
                Id = DebtCollectId, SystemKey = "DEBT_COLLECT", Name = "Thu nợ",
                Type = CategoryType.Debt, AppliesToAllWallets = true,
                CreatedAt = SystemCategorySeedTime, UpdatedAt = SystemCategorySeedTime
            },
            new Category
            {
                Id = DebtBorrowId, SystemKey = "DEBT_BORROW", Name = "Đi vay",
                Type = CategoryType.Debt, AppliesToAllWallets = true,
                CreatedAt = SystemCategorySeedTime, UpdatedAt = SystemCategorySeedTime
            },
            new Category
            {
                Id = DebtRepayId, SystemKey = "DEBT_REPAY", Name = "Trả nợ",
                Type = CategoryType.Debt, AppliesToAllWallets = true,
                CreatedAt = SystemCategorySeedTime, UpdatedAt = SystemCategorySeedTime
            }
        );
    }
}

public class WalletCategoryConfiguration : IEntityTypeConfiguration<WalletCategory>
{
    public void Configure(EntityTypeBuilder<WalletCategory> b)
    {
        b.ToTable("wallet_categories");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.WalletId, x.CategoryId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        b.HasIndex(x => new { x.UserId, x.UpdatedAt });

        b.HasOne(x => x.Wallet)
            .WithMany(w => w.WalletCategories)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Category)
            .WithMany(c => c.WalletCategories)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(true);
    }
}
