using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SpotifyClientService;
using SpotifyGenreOrganizer;
using SpotifyGenreOrganizer.UI;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Data.Repositories.Implementations;
using SpotifyTools.Sync;
using SpotifyTools.Analytics;

// Configure Serilog early
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Starting Spotify Tools application");
    
    var host = CreateHostBuilder(args).Build();

    // Run the CLI menu
    var cliMenu = host.Services.GetRequiredService<CliMenuService>();
    await cliMenu.RunAsync();
    
    Log.Information("Application stopped normally");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext())
        .ConfigureAppConfiguration((context, config) =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;

            // Database
            var connectionString = configuration.GetConnectionString("SpotifyDatabase");
            services.AddDbContext<SpotifyDbContext>(options =>
                options.UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention());

            // Data layer
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Spotify client
            services.AddSingleton<ISpotifyClientService, SpotifyClientWrapper>();

            // Sync service
            services.AddScoped<ISyncService, SyncService>();

            // Analytics service
            services.AddScoped<IAnalyticsService, AnalyticsService>();

            // Navigation service
            services.AddScoped<NavigationService>();

            // CLI
            services.AddScoped<CliMenuService>();
        });
