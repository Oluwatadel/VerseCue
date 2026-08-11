using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using Versecue.Application.Interfaces;
using Versecue.Application.Interfaces.Repository;
using Versecue.Application.Services;
using Versecue.Infrastructure.Audio;
using Versecue.Infrastructure.Llm;
using Versecue.Infrastructure.Persistence;
using Versecue.Infrastructure.Services;
using Versecue.Infrastructure.Stt;
using DapperBibleRepository = Versecue.Infrastructure.Persistence.DapperBibleRepository;

namespace Versecue.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration, String? connectionString = null)
    {
        connectionString ??= configuration.GetConnectionString("VerseCue")
            ?? throw new InvalidOperationException("Connection string 'VerseCue' is not configured.");

        //services.AddSingleton<Func<DbConnection>>(_ =>
        //    () => new SqliteConnection(connectionString));

        services.AddDbContext<VersecueDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddSingleton<IBibleRepository, DapperBibleRepository>();

        services.AddScoped<IBibleImportService, BibleImportService>();

        var whisperOptions =
            configuration
                .GetSection("Whisper")
                .Get<WhisperOptions>()
            ?? new WhisperOptions();

        var audioOptions =
            configuration
                .GetSection("Audio")
                .Get<AudioOptions>()
            ?? new AudioOptions();

        services.AddSingleton(whisperOptions);
        services.AddSingleton(audioOptions);

        services.AddSingleton<WhisperEngine>();

        services.AddSingleton<
            IWhisperTranscriptionService,
            WhisperTranscriptionService>();

        services.AddSingleton<
            IAudioCaptureService,
            NAudioCaptureService>();

        services.AddSingleton<
            IVerseDetectionService,
            VerseDetectionService>();

        services.AddSingleton<
            ILlmService,
            LlmService>();

        services.AddSingleton<VerseCueService>();

        services.AddSingleton<IBibleReferenceService, BibleReferenceService>();

        return services;
    }
}