using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace StageFlow.Infrastructure.Persistence;

public class StageFlowDbContextFactory : IDesignTimeDbContextFactory<StageFlowDbContext>
{
    public StageFlowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StageFlowDbContext>();

        // Fallback for EF Core migrations design-time
        var connectionString = "Host=localhost;Port=5432;Database=stageflow_db;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(StageFlowDbContext).Assembly.FullName));

        return new StageFlowDbContext(optionsBuilder.Options);
    }
}

