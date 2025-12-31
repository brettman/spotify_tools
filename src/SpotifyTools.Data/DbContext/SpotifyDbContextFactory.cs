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
        var connectionString = "Host=localhost;Port=5433;Database=spotify_tools;Username=spotify_user;Password=my_s3cur3_p455w0rd!";

        optionsBuilder.UseNpgsql(connectionString);

        return new SpotifyDbContext(optionsBuilder.Options);
    }
}
