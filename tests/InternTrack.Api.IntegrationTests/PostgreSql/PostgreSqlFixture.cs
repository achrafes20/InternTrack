using Microsoft.EntityFrameworkCore;
using Npgsql;
using InternTrack.Domain.Entities;
using InternTrack.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace InternTrack.Api.IntegrationTests.PostgreSql;

/// <summary>
/// Démarre un vrai serveur PostgreSQL 16 (même image que docker-compose) dans un conteneur Docker,
/// partagé par toutes les classes de la collection « PostgreSQL ». Chaque test utilise sa propre base
/// pour rester indépendant des autres.
/// Prérequis : Docker doit être disponible (Docker Desktop en local, natif sur les runners GitHub Actions).
/// Pour exclure ces tests : dotnet test --filter "Category!=PostgreSQL".
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string ConnectionString(string database)
        => new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    public InternTrackDbContext CreateContext(string database)
        => new(new DbContextOptionsBuilder<InternTrackDbContext>()
            .UseNpgsql(ConnectionString(database))
            .Options);

    /// <summary>Crée une base vierge et y applique toutes les migrations (comme au démarrage de l'API).</summary>
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        var database = $"interntrack_{Guid.NewGuid():N}";
        await using var context = CreateContext(database);
        await context.Database.MigrateAsync();
        return database;
    }

    /// <summary>Jeu de données minimal respectant les clés étrangères : une filière, une entreprise, des stages en cours.</summary>
    public static async Task<(AcademicTrack Track, Company Company, List<Internship> Internships)> SeedInProgressInternshipsAsync(
        InternTrackDbContext context, int count)
    {
        var track = new AcademicTrack($"GI{Guid.NewGuid():N}"[..10], "Génie Informatique", "");
        var company = new Company("Tech Maroc", "IT", "Casablanca", "Adresse", "contact@tech.ma");
        context.AddRange(track, company);

        var internships = Enumerable.Range(1, count).Select(i =>
        {
            var internship = new Internship($"Stage {i}", "Description", DateTime.UtcNow, DateTime.UtcNow.AddMonths(4),
                $"student-{i}", company.Id, track.Id, "Tuteur", "tuteur@tech.ma");
            internship.SubmitForApproval($"student-{i}");
            internship.Approve("head");
            internship.StartInternship("head");
            return internship;
        }).ToList();

        context.Internships.AddRange(internships);
        await context.SaveChangesAsync();
        return (track, company, internships);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
