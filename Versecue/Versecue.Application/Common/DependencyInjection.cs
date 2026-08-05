using Microsoft.Extensions.DependencyInjection;
using Versecue.Application.Interfaces;
using Versecue.Application.UseCases;

namespace Versecue.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<BibleReferenceNormalizationService>();
        services.AddScoped<BibleReferenceDetectionService>();

        return services;
    }
}