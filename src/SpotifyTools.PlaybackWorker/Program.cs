using Microsoft.EntityFrameworkCore;
using Serilog;
using SpotifyClientService;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories;
using SpotifyTools.Data.Repositories.Implementations;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.PlaybackWorker;
using SpotifyTools.Sync;

// Configure Serilog early logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Spotify Playback Worker Service");

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog from appsettings.json
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Database - PostgreSQL with snake_case naming
    var connectionString = builder.Configuration.GetConnectionString("SpotifyDatabase");
    builder.Services.AddDbContext<SpotifyDbContext>(options =>
        options.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

    // Spotify client service
    builder.Services.AddSingleton<ISpotifyClientService, SpotifyClientWrapper>();

    // Unit of Work and Repositories
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<ISyncStateRepository, SyncStateRepository>();

    // Sync services
    builder.Services.AddScoped<ISyncService, SyncService>();
    builder.Services.AddScoped<IncrementalSyncOrchestrator>();

    // Hosted service for playback tracking
    builder.Services.AddHostedService<PlaybackTracker>();
    
    // Hosted service for sync operations
    builder.Services.AddHostedService<SyncWorker>();

    var host = builder.Build();

    Log.Information("Playback Worker Service configured successfully");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Playback Worker Service terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
