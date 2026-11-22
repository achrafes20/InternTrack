using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace InternTrack.Infrastructure.Persistence;

public class InternTrackDbContextFactory : IDesignTimeDbContextFactory<InternTrackDbContext>
{
    public InternTrackDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InternTrackDbContext>();

        // Fallback for EF Core migrations design-time
        var connectionString = "Host=localhost;Port=5432;Database=interntrack_db;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(InternTrackDbContext).Assembly.FullName));

        return new InternTrackDbContext(optionsBuilder.Options);
    }
}

