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

        // ---------------------------------------------------------
        // Bible Reference and Detection Services
        // ---------------------------------------------------------

        services.AddScoped<IBibleReferenceService, Versecue.Infrastructure.Services.BibleReferenceService>();
        services.AddScoped<IVerseDetectionService, Versecue.Infrastructure.Services.VerseDetectionService>();

        // ---------------------------------------------------------
        // Audio & STT Services
        // ---------------------------------------------------------
        
        services.AddSingleton(new Versecue.Infrastructure.Audio.AudioOptions());
        services.AddSingleton(new Versecue.Infrastructure.Stt.WhisperOptions());

        services.AddSingleton<Versecue.Application.Interfaces.IAudioCaptureService, Versecue.Infrastructure.Audio.NAudioCaptureService>();
        services.AddSingleton<Versecue.Infrastructure.Stt.WhisperEngine>();
        services.AddSingleton<Versecue.Application.Interfaces.IWhisperTranscriptionService, Versecue.Infrastructure.Stt.WhisperTranscriptionService>();

        // ---------------------------------------------------------
        // AI Services
        // ---------------------------------------------------------
        services.AddSingleton<Versecue.Application.Interfaces.ILlmService, Versecue.Infrastructure.Llm.LlmService>();

        // ---------------------------------------------------------
        // VerseCue Orchestration
        // ---------------------------------------------------------
        services.AddSingleton<Versecue.Application.Services.VerseCueService>();

        return services;
    }
}