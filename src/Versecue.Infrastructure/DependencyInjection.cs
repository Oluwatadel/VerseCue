using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Versecue.Application.Interfaces;
using Versecue.Application.Services;
using Versecue.Infrastructure.Audio;
using Versecue.Infrastructure.Llm;
using Versecue.Infrastructure.Persistence;
using Versecue.Infrastructure.Services;
using Versecue.Infrastructure.Stt;

namespace Versecue.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VerseCue")
            ?? "Data Source=versecue.db";

        services.AddDbContext<VersecueDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

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