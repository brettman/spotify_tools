using Microsoft.EntityFrameworkCore;
using SpotifyClientService;
using SpotifyTools.Analytics;
using SpotifyTools.Data.DbContext;
using SpotifyTools.Data.Repositories.Implementations;
using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Sync;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

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
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
