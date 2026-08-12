using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Versecue.Application.Interfaces;
using Versecue.Application.Interfaces.Repository;
using Versecue.Infrastructure.Persistence;

namespace Versecue.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("VerseCue")
            ?? throw new InvalidOperationException(
                "Connection string 'VerseCue' is not configured.");

        // ---------------------------------------------------------
        // Entity Framework Core
        // ---------------------------------------------------------

        services.AddDbContext<VersecueDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        // ---------------------------------------------------------
        // Bible import
        // ---------------------------------------------------------

        services.AddScoped<IBibleImportService, BibleImportService>();

        // ---------------------------------------------------------
        // Bible repository - Dapper
        // ---------------------------------------------------------

        services.AddScoped<IBibleRepository, DapperBibleRepository>();

        return services;
    }
}