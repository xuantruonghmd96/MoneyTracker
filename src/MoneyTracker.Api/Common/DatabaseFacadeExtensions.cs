using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MoneyTracker.Api.Services.Exceptions;

namespace MoneyTracker.Api.Common;

public static class DatabaseFacadeExtensions
{
    /// <summary>
    /// Acquires a PostgreSQL transaction-scoped advisory lock for the given key.
    /// MUST be called inside an explicit transaction (BeginTransactionAsync) —
    /// pg_advisory_xact_lock releases immediately without a transaction.
    /// Throws ServiceBusyException (503) if lock cannot be acquired within 5 seconds.
    /// No-op on non-relational providers (e.g. EF Core in-memory used in unit tests).
    /// </summary>
    public static async Task AcquireAdvisoryLockAsync(
        this DatabaseFacade database, Guid key, CancellationToken ct = default)
    {
        if (!database.IsRelational()) return;
        try
        {
            await database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5000'", ct);
            await database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(key.ToByteArray(), 0)})", ct);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "55P03")
        {
            throw new ServiceBusyException();
        }
    }
}
