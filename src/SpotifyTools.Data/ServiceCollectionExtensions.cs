using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SpotifyTools.Data.Repositories.Implementations;
using SpotifyTools.Data.Repositories.Interfaces;

namespace SpotifyTools.Data;

/// <summary>
/// Extension methods for registering data layer services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register data layer services (DbContext, repositories, Unit of Work)
    /// </summary>
    public static IServiceCollection AddSpotifyDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with PostgreSQL
        var connectionString = configuration.GetConnectionString("SpotifyDatabase")
            ?? throw new InvalidOperationException("SpotifyDatabase connection string is not configured");

        services.AddDbContext<DbContext.SpotifyDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register individual repositories (if needed outside of UnitOfWork)
        services.AddScoped<ITrackRepository, TrackRepository>();

        return services;
    }
}
