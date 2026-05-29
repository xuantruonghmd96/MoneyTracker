using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyTracker.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("transactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
        b.Property(x => x.OccurredAt).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2048);

        // Indexes cho list + report
        b.HasIndex(x => new { x.UserId, x.OccurredAt });
        b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        b.HasIndex(x => new { x.WalletId, x.OccurredAt });
        b.HasIndex(x => new { x.CategoryId, x.OccurredAt });
        b.HasIndex(x => new { x.UserId, x.DeletedAt });

        b.HasOne(x => x.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(true);

        b.HasOne(x => x.Wallet)
            .WithMany(w => w.Transactions)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Participant)
            .WithMany()
            .HasForeignKey(x => x.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        b.HasIndex(x => new { x.ParticipantId, x.OccurredAt })
            .HasFilter("participant_id IS NOT NULL")
            .HasDatabaseName("ix_transactions_participant_id_occurred_at");
    }
}

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> b)
    {
        b.ToTable("households");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);

        b.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> b)
    {
        b.ToTable("household_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();

        b.HasIndex(x => new { x.HouseholdId, x.UserId, x.Status });
        b.HasIndex(x => new { x.UserId, x.Status });

        b.HasOne(x => x.Household)
            .WithMany(h => h.Members)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany(u => u.HouseholdMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> b)
    {
        b.ToTable("household_invitations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.InviteeEmail).HasMaxLength(256);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Household)
            .WithMany(h => h.Invitations)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.InviterUser)
            .WithMany()
            .HasForeignKey(x => x.InviterUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HouseholdCategoryShareConfiguration : IEntityTypeConfiguration<HouseholdCategoryShare>
{
    public void Configure(EntityTypeBuilder<HouseholdCategoryShare> b)
    {
        b.ToTable("household_category_shares");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.HouseholdMemberId, x.CategoryId });

        b.HasOne(x => x.HouseholdMember)
            .WithMany(m => m.CategoryShares)
            .HasForeignKey(x => x.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HouseholdCategoryShareTargetConfiguration : IEntityTypeConfiguration<HouseholdCategoryShareTarget>
{
    public void Configure(EntityTypeBuilder<HouseholdCategoryShareTarget> b)
    {
        b.ToTable("household_category_share_targets");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.ShareId, x.TargetHouseholdMemberId }).IsUnique();

        b.HasOne(x => x.Share)
            .WithMany(s => s.Targets)
            .HasForeignKey(x => x.ShareId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.TargetHouseholdMember)
            .WithMany()
            .HasForeignKey(x => x.TargetHouseholdMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
