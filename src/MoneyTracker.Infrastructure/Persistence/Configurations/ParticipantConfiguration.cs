using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoneyTracker.Infrastructure.Persistence.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> b)
    {
        b.ToTable("participants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        b.Property(x => x.Note).HasMaxLength(512);

        // Unique per user (case-sensitive), chỉ khi chưa xóa
        b.HasIndex(x => new { x.UserId, x.Name })
            .HasFilter("deleted_at IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_participants_user_id_name");

        // Mỗi user chỉ có đúng 1 default participant
        b.HasIndex(x => x.UserId)
            .HasFilter("is_default = true AND deleted_at IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_participants_user_id_default");

        b.HasIndex(x => new { x.UserId, x.UpdatedAt });

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}

public class ParticipantLinkConfiguration : IEntityTypeConfiguration<ParticipantLink>
{
    public void Configure(EntityTypeBuilder<ParticipantLink> b)
    {
        b.ToTable("participant_links");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.ParticipantAId, x.ParticipantBId })
            .HasFilter("deleted_at IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_participant_links_pair");

        b.HasIndex(x => x.HouseholdId);
    }
}

public class SyncBatchConfiguration : IEntityTypeConfiguration<SyncBatch>
{
    public void Configure(EntityTypeBuilder<SyncBatch> b)
    {
        b.ToTable("sync_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.ResponseJson).IsRequired();

        b.HasIndex(x => new { x.UserId, x.ProcessedAt });
    }
}

public class TransactionAuditConfiguration : IEntityTypeConfiguration<TransactionAudit>
{
    public void Configure(EntityTypeBuilder<TransactionAudit> b)
    {
        b.ToTable("transaction_audits");
        b.HasKey(x => x.Id);
        b.Property(x => x.Operation).IsRequired().HasMaxLength(16);
        b.Property(x => x.SnapshotJson).IsRequired();
        b.Property(x => x.ActorDevice).HasMaxLength(128);

        b.HasIndex(x => x.TransactionId);
        b.HasIndex(x => new { x.UserId, x.OccurredAt });
        // Không có FK trên TransactionId (giữ được khi transaction soft-deleted)
    }
}

public class HouseholdWalletShareConfiguration : IEntityTypeConfiguration<HouseholdWalletShare>
{
    public void Configure(EntityTypeBuilder<HouseholdWalletShare> b)
    {
        b.ToTable("household_wallet_shares");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.HouseholdMemberId, x.WalletId });

        b.HasOne(x => x.HouseholdMember)
            .WithMany()
            .HasForeignKey(x => x.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Wallet)
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HouseholdWalletShareTargetConfiguration : IEntityTypeConfiguration<HouseholdWalletShareTarget>
{
    public void Configure(EntityTypeBuilder<HouseholdWalletShareTarget> b)
    {
        b.ToTable("household_wallet_share_targets");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.ShareId, x.TargetHouseholdMemberId })
            .IsUnique();

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
