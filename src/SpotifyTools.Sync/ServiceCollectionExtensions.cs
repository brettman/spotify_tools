using Microsoft.Extensions.DependencyInjection;

namespace SpotifyTools.Sync;

/// <summary>
/// Extension methods for registering sync services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers sync services with the dependency injection container
    /// </summary>
    public static IServiceCollection AddSyncServices(this IServiceCollection services)
    {
        services.AddScoped<ISyncService, SyncService>();

        return services;
    }
}
