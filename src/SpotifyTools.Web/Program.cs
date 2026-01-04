using Microsoft.EntityFrameworkCore;
using SpotifyClientService;
using SpotifyTools.Analytics;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Implementations;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Sync;
using SpotifyTools.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Spotify Tools API",
        Version = "v1",
        Description = "API for managing Spotify library, playlists, and genre clusters"
    });
});

// Database - use connection string from appsettings
var connectionString = builder.Configuration.GetConnectionString("SpotifyDatabase");
builder.Services.AddDbContext<SpotifyDbContext>(options =>
    options.UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

// Data layer - Unit of Work and Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Spotify client service
builder.Services.AddSingleton<ISpotifyClientService, SpotifyClientWrapper>();

// Sync service
builder.Services.AddScoped<ISyncService, SyncService>();

// Analytics service
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Business logic services (Phase 1 refactoring)
builder.Services.AddScoped<IGenreService, GenreService>();
builder.Services.AddScoped<IPlaylistService, PlaylistService>();
builder.Services.AddScoped<IPlayHistoryService, PlayHistoryService>();
builder.Services.AddScoped<IListeningAnalyticsService, ListeningAnalyticsService>();

// Background services
builder.Services.AddHostedService<PlaybackTrackingService>();

// API Client Service for Blazor (will be removed in Phase 1)
// Keeping for now to maintain backwards compatibility during refactoring
builder.Services.AddHttpClient<ApiClientService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5241/");  // Self-reference
});

// CORS (for future frontend clients)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Spotify Tools API v1");
        options.RoutePrefix = "swagger"; // Swagger at /swagger
    });
}

// Disable HTTPS redirection for now (development only)
// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAntiforgery();

// Map API controllers
app.MapControllers();

// Map Blazor components
app.MapRazorComponents<SpotifyTools.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
