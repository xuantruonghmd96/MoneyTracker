using MoneyTracker.Infrastructure.Auth;
using MoneyTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MoneyTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseNpgsql(connStr, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            opts.UseSnakeCaseNamingConvention();
        });

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}
