using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using MoneyTracker.Infrastructure.Persistence;

namespace MoneyTracker.Api.Tests.Helpers;

public static class DbContextFactory
{
    public static AppDbContext Create() => CreateNamed(Guid.NewGuid().ToString());

    public static AppDbContext CreateNamed(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}
