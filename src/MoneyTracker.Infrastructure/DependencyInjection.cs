using MoneyTracker.Infrastructure.Auth;
using MoneyTracker.Infrastructure.Persistence;
using MoneyTracker.Infrastructure.Persistence.Interceptors;
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

        // Interceptor phải đăng ký trước AddDbContext để resolve được
        services.AddScoped<TransactionAuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connStr, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            opts.UseSnakeCaseNamingConvention();
            opts.AddInterceptors(sp.GetRequiredService<TransactionAuditInterceptor>());
        });

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}
