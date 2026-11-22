using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;
using InternTrack.Infrastructure.Identity;
using InternTrack.Infrastructure.Persistence.Interceptors;
using InternTrack.Infrastructure.Persistence.Outbox;
using InternTrack.Infrastructure.Services.Email;

namespace InternTrack.Infrastructure.Persistence;

public class InternTrackDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly AuditableEntityInterceptor? _auditableEntityInterceptor;
    private readonly AuditLedgerInterceptor? _auditLedgerInterceptor;
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public InternTrackDbContext(
        DbContextOptions<InternTrackDbContext> options,
        AuditableEntityInterceptor? auditableEntityInterceptor = null,
        AuditLedgerInterceptor? auditLedgerInterceptor = null,
        IDomainEventDispatcher? domainEventDispatcher = null,
        EmailOutboxSignal? outboxSignal = null) : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
        _auditLedgerInterceptor = auditLedgerInterceptor;
        _domainEventDispatcher = domainEventDispatcher;
        _outboxSignal = outboxSignal;
    }

    private readonly EmailOutboxSignal? _outboxSignal;

    /// <summary>E-mails en attente d'envoi (outbox transactionnelle).</summary>
    public DbSet<EmailOutboxMessage> EmailOutbox => Set<EmailOutboxMessage>();

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
    public DbSet<DefenseEvaluation> DefenseEvaluations => Set<DefenseEvaluation>();
    public DbSet<ConventionAmendment> ConventionAmendments => Set<ConventionAmendment>();
    public DbSet<InternshipDocument> InternshipDocuments => Set<InternshipDocument>();

    /// <summary>Nom de la propriété fantôme mappée sur la colonne système PostgreSQL « xmin ».</summary>
    public const string ConcurrencyTokenProperty = "Version";

    // Événements produits par les sauvegardes en cours : publiés avant le COMMIT, dans la même transaction.
    private readonly List<IDomainEvent> _pendingDomainEvents = new();
    private bool _deferDomainEvents;
    private bool _outboxWritten;

    /// <summary>
    /// Vrai pendant la publication des événements de domaine : les e-mails produits par les handlers sont
    /// ajoutés au suivi puis enregistrés par le DbContext dans la transaction de la modification métier.
    /// </summary>
    public bool IsDispatchingDomainEvents { get; private set; }

    /// <summary>
    /// Enregistre les changements puis publie les événements de domaine <b>dans la même transaction</b> :
    /// les e-mails des notifications (outbox) sont validés avec la modification métier, jamais sans elle.
    /// Une erreur dans les notifications est annulée seule (point de sauvegarde) : la modification métier est conservée.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_deferDomainEvents || IsDispatchingDomainEvents || _domainEventDispatcher == null || !HasDomainEvents())
        {
            var saved = await SaveAndCollectEventsAsync(cancellationToken);
            NotifyOutboxIfCommitted();
            return saved;
        }

        var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;

        var result = await SaveAndCollectEventsAsync(cancellationToken);
        await DispatchPendingEventsAsync(cancellationToken);

        if (transaction != null)
            await transaction.CommitAsync(cancellationToken);

        NotifyOutboxIfCommitted();
        return result;
    }

    private bool HasDomainEvents()
        => ChangeTracker.Entries<BaseEntity>().Any(e => e.Entity.DomainEvents.Count > 0);

    private async Task<int> SaveAndCollectEventsAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
        var writesOutbox = ChangeTracker.Entries<EmailOutboxMessage>().Any(e => e.State == EntityState.Added);

        var result = await base.SaveChangesAsync(cancellationToken);

        // Effacés après la sauvegarde : en cas d'échec (conflit de concurrence…), ils ne sont pas perdus.
        entitiesWithEvents.ForEach(e => e.ClearDomainEvents());
        if (_domainEventDispatcher != null)
            _pendingDomainEvents.AddRange(domainEvents);
        if (writesOutbox)
            _outboxWritten = true;

        return result;
    }

    private async Task DispatchPendingEventsAsync(CancellationToken cancellationToken)
    {
        if (_domainEventDispatcher == null || _pendingDomainEvents.Count == 0)
            return;

        const string savepoint = "domain_events";
        var transaction = Database.IsRelational() ? Database.CurrentTransaction : null;
        if (transaction != null)
            await transaction.CreateSavepointAsync(savepoint, cancellationToken);

        IsDispatchingDomainEvents = true;
        try
        {
            // Un handler peut lui-même produire des événements : on traite par vagues (bornées).
            for (var round = 0; round < 5 && _pendingDomainEvents.Count > 0; round++)
            {
                var batch = _pendingDomainEvents.ToList();
                _pendingDomainEvents.Clear();
                await _domainEventDispatcher.DispatchAsync(batch, cancellationToken);
                await SaveAndCollectEventsAsync(cancellationToken);
            }
            _pendingDomainEvents.Clear();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (transaction != null)
                await transaction.RollbackToSavepointAsync(savepoint, cancellationToken);

            foreach (var entry in ChangeTracker.Entries<EmailOutboxMessage>().ToList())
                entry.State = EntityState.Detached;
            _pendingDomainEvents.Clear();

            try
            {
                this.GetService<ILoggerFactory>().CreateLogger<InternTrackDbContext>()
                    .LogError(ex, "Échec de l'enregistrement des notifications : la modification métier est conservée.");
            }
            catch
            {
                // Journalisation indisponible : on n'empêche pas la validation.
            }
        }
        finally
        {
            IsDispatchingDomainEvents = false;
        }
    }

    private void NotifyOutboxIfCommitted()
    {
        if (!_outboxWritten || _deferDomainEvents || IsDispatchingDomainEvents)
            return;
        if (Database.IsRelational() && Database.CurrentTransaction != null)
            return;

        _outboxWritten = false;
        _outboxSignal?.Notify();
    }

    /// <summary>
    /// Exécute <paramref name="action"/> dans une transaction protégée par un verrou applicatif PostgreSQL
    /// (pg_advisory_xact_lock) : deux opérations portant le même nom de verrou s'exécutent l'une après l'autre,
    /// y compris sur plusieurs instances de l'API. Le verrou est libéré automatiquement à la fin de la transaction.
    /// Les événements produits sont publiés avant le COMMIT (outbox dans la même transaction).
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

            _deferDomainEvents = false;
            await DispatchPendingEventsAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            NotifyOutboxIfCommitted();
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

        // Les identifiants sont générés par le domaine (Guid.NewGuid) : EF ne doit pas les considérer comme
        // « générés par la base ». Sans cela, une entité ajoutée via une navigation (ex. une évaluation ajoutée
        // à Defense.Evaluations) serait prise pour une ligne existante et mise à jour au lieu d'être insérée.
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => !t.IsOwned() && typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            builder.Entity(entityType.ClrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
        }

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
