using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Versecue.Application.Interfaces;
using Versecue.Infrastructure.Persistence.Ef;
using Versecue.Infrastructure.Persistence.Dapper;
using Versecue.Infrastructure.Audio;
using Versecue.Infrastructure.Stt;
using Versecue.Infrastructure.Llm;
using Versecue.Infrastructure.Persistence.Import;

namespace Versecue.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure layer services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connStr = configuration.GetConnectionString("Default") 
            ?? "Data Source=versecue.db";

        // EF Core DbContext for mutable data
        services.AddDbContext<VersecueDbContext>((sp, options) =>
        {
            options.UseSqlite(connStr);
        });

        // Dapper Bible Repository for reference data (read-only, high performance)
        services.AddScoped<IBibleRepository, DapperBibleRepository>();
        services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(connStr));

        // Register active infrastructure services
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<ISttService, SttService>();
        services.AddSingleton<ILlmService, LlmService>();
        services.AddScoped<BibleImportService>();

        return services;
    }
}