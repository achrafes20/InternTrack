using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Infrastructure.Identity;
using StageFlow.Infrastructure.Persistence.Interceptors;

namespace StageFlow.Infrastructure.Persistence;

public class StageFlowDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly AuditableEntityInterceptor? _auditableEntityInterceptor;
    private readonly AuditLedgerInterceptor? _auditLedgerInterceptor;
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public StageFlowDbContext(
        DbContextOptions<StageFlowDbContext> options,
        AuditableEntityInterceptor? auditableEntityInterceptor = null,
        AuditLedgerInterceptor? auditLedgerInterceptor = null,
        IDomainEventDispatcher? domainEventDispatcher = null) : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
        _auditLedgerInterceptor = auditLedgerInterceptor;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public DbSet<AcademicTrack> AcademicTracks => Set<AcademicTrack>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<InternshipOffer> InternshipOffers => Set<InternshipOffer>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<Convention> Conventions => Set<Convention>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<ReportComment> ReportComments => Set<ReportComment>();
    public DbSet<Defense> Defenses => Set<Defense>();
    public DbSet<DefenseJuryMember> DefenseJuryMembers => Set<DefenseJuryMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Nom de la propriété fantôme mappée sur la colonne système PostgreSQL « xmin ».</summary>
    public const string ConcurrencyTokenProperty = "Version";

    // Événements produits pendant une transaction explicite : publiés seulement après le COMMIT.
    private readonly List<IDomainEvent> _pendingDomainEvents = new();
    private bool _deferDomainEvents;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Les événements sont collectés avant la sauvegarde puis publiés une fois celle-ci réussie :
        // aucun e-mail n'est envoyé pour une transaction qui aurait échoué.
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Effacés après la sauvegarde : en cas d'échec (conflit de concurrence…), ils ne sont pas perdus.
        entitiesWithEvents.ForEach(e => e.ClearDomainEvents());

        if (_deferDomainEvents)
        {
            _pendingDomainEvents.AddRange(domainEvents);
        }
        else if (_domainEventDispatcher != null && domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Exécute <paramref name="action"/> dans une transaction protégée par un verrou applicatif PostgreSQL
    /// (pg_advisory_xact_lock) : deux opérations portant le même nom de verrou s'exécutent l'une après l'autre,
    /// y compris sur plusieurs instances de l'API. Le verrou est libéré automatiquement à la fin de la transaction.
    /// Sur un fournisseur sans transactions (base en mémoire des tests), l'action est exécutée directement.
    /// </summary>
    public async Task<T> RunExclusiveAsync<T>(string lockName, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction != null)
            return await action();

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        _deferDomainEvents = true;
        try
        {
            var lockKey = StableLockKey(lockName);
            await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);

            var result = await action();
            await transaction.CommitAsync(cancellationToken);

            _deferDomainEvents = false;
            var committedEvents = _pendingDomainEvents.ToList();
            _pendingDomainEvents.Clear();

            if (_domainEventDispatcher != null && committedEvents.Count > 0)
                await _domainEventDispatcher.DispatchAsync(committedEvents, cancellationToken);

            return result;
        }
        catch
        {
            _deferDomainEvents = false;
            _pendingDomainEvents.Clear();
            throw;
        }
    }

    /// <summary>
    /// Hachage FNV-1a 64 bits : stable d'un processus à l'autre (contrairement à string.GetHashCode),
    /// donc identique sur toutes les instances de l'API.
    /// </summary>
    private static long StableLockKey(string name)
    {
        unchecked
        {
            var hash = 14695981039346656037UL;
            foreach (var c in name)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return (long)hash;
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_auditableEntityInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        }

        if (_auditLedgerInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditLedgerInterceptor);
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Concurrence optimiste : chaque entité métier modifiable porte la colonne système PostgreSQL « xmin »
        // comme jeton. Si deux utilisateurs modifient la même ligne, le second SaveChanges échoue
        // (DbUpdateConcurrencyException → HTTP 409) au lieu d'écraser silencieusement le premier.
        // Le journal d'audit est exclu : il n'est jamais modifié (trigger append-only).
        if (Database.IsNpgsql())
        {
            var concurrencyTracked = builder.Model.GetEntityTypes()
                .Where(t => !t.IsOwned()
                            && typeof(BaseEntity).IsAssignableFrom(t.ClrType)
                            && t.ClrType != typeof(AuditLog))
                .Select(t => t.ClrType)
                .ToList();

            foreach (var clrType in concurrencyTracked)
            {
                builder.Entity(clrType).Property<uint>(ConcurrencyTokenProperty).IsRowVersion();
            }
        }
    }
}
