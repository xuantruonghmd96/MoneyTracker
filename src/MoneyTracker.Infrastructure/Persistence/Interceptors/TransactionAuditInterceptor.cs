using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyTracker.Domain.Common;
using MoneyTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MoneyTracker.Infrastructure.Persistence.Interceptors;

public class TransactionAuditInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions _snapshotOpts = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private readonly ICurrentActorContext _actor;

    public TransactionAuditInterceptor(ICurrentActorContext actor)
    {
        _actor = actor;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not AppDbContext db)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var audits = new List<TransactionAudit>();

        foreach (var entry in db.ChangeTracker.Entries<Transaction>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var originalDeletedAt = entry.State == EntityState.Modified
                ? entry.OriginalValues[nameof(Transaction.DeletedAt)] as DateTimeOffset?
                : null;

            var op = entry.State == EntityState.Added ? "create"
                : entry.Entity.DeletedAt != null && originalDeletedAt == null ? "delete"
                : "update";

            audits.Add(new TransactionAudit
            {
                Id = Guid.NewGuid(),
                TransactionId = entry.Entity.Id,
                UserId = entry.Entity.UserId ?? Guid.Empty,
                Operation = op,
                SnapshotJson = JsonSerializer.Serialize(entry.Entity, _snapshotOpts),
                ActorUserId = _actor.ActorUserId,
                ActorDevice = _actor.ActorDeviceId,
                OccurredAt = now
            });
        }

        if (audits.Count > 0)
            db.TransactionAudits.AddRange(audits);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
