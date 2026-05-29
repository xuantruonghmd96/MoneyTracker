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
            .OnDelete(DeleteBehavior.Cascade);

        // Constraint: ví Credit phải có CreditLimit
        b.ToTable(t => t.HasCheckConstraint(
            "ck_wallets_credit_limit",
            "(type = 0) OR (type = 1 AND credit_limit IS NOT NULL)"));
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.AppliesToAllWallets).HasDefaultValue(false);
        b.Property(x => x.Icon).HasMaxLength(64);
        b.Property(x => x.Color).HasMaxLength(16);

        b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        b.HasIndex(x => new { x.UserId, x.ParentId });
        b.HasIndex(x => new { x.UserId, x.DeletedAt });

        b.HasOne(x => x.User)
            .WithMany(u => u.Categories)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
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
            .OnDelete(DeleteBehavior.Cascade);
    }
}
