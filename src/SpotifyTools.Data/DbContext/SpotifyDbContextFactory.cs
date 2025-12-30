using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpotifyTools.Data.DbContext;

/// <summary>
/// Design-time factory for creating DbContext during migrations
/// This allows EF Core tools to create the context without running the application
/// </summary>
public class SpotifyDbContextFactory : IDesignTimeDbContextFactory<SpotifyDbContext>
{
    public SpotifyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SpotifyDbContext>();

        // Default connection string for migrations
        // This will be overridden by appsettings.json in the actual application
        var connectionString = "Host=localhost;Port=5432;Database=spotify_tools;Username=spotify_user;Password=spotify_dev_password";

        optionsBuilder.UseNpgsql(connectionString);

        return new SpotifyDbContext(optionsBuilder.Options);
    }
}
