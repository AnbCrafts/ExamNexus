using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExamNexus.Application.Interfaces;
using ExamNexus.Domain.Entities;
using ExamNexus.Infrastructure.Data;
using ExamNexus.Infrastructure.Services;

namespace ExamNexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var useSqlServer = configuration.GetValue<bool>("DatabaseSettings:UseSqlServer");
        var connectionString = useSqlServer
            ? (configuration.GetConnectionString("SqlServerConnection") ?? configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
            : (configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ExamNexus.db");

        // Automatically ensure parent directory exists if using file-based SQLite (e.g. on Docker persistent disk mounts)
        if (!useSqlServer && connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var filePath = connectionString.Split('=', 2)[1].Trim().Trim(';', '"');
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch
            {
                // Ignore if not a standard file path
            }
        }

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (useSqlServer)
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "ExamSaaS");
                });
            }
            else
            {
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                });
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => 
            provider.GetRequiredService<ApplicationDbContext>());

        // Identity Configuration with hardened rules
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Infrastructure Services
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
