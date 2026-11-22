using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InternTrack.Infrastructure.Persistence;
using InternTrack.Infrastructure.Persistence.Outbox;

namespace InternTrack.Infrastructure.Services.Email;

/// <summary>
/// Envoie les e-mails de la table EmailOutbox. Chaque message est d'abord réservé (colonne LockedUntil,
/// « FOR UPDATE SKIP LOCKED ») : plusieurs instances de l'API peuvent tourner sans envoyer deux fois le même e-mail.
/// En cas d'échec, le message est réessayé avec un délai croissant, puis marqué « Failed » après
/// <see cref="EmailOutboxMessage.MaxAttempts"/> essais (visible dans l'administration, réactivable).
/// </summary>
public class EmailOutboxProcessor : BackgroundService
{
    public static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SentRetention = TimeSpan.FromDays(30);
    public static readonly TimeSpan FailedRetention = TimeSpan.FromDays(90);
    public const int BatchSize = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly EmailOutboxSignal _signal;
    private readonly ILogger<EmailOutboxProcessor> _logger;
    private readonly TimeProvider _timeProvider;
    private DateTime _lastPurge = DateTime.MinValue;

    public EmailOutboxProcessor(
        IServiceProvider serviceProvider,
        EmailOutboxSignal signal,
        ILogger<EmailOutboxProcessor> logger,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _signal = signal;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Tant qu'un lot est plein, il reste probablement des messages : on enchaîne.
                while (await ProcessBatchAsync(stoppingToken) == BatchSize) { }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (now - _lastPurge > TimeSpan.FromHours(1))
                {
                    await PurgeAsync(stoppingToken);
                    _lastPurge = now;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur du processeur d'e-mails (outbox). Nouvel essai au prochain cycle.");
            }

            try
            {
                await _signal.WaitAsync(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Envoie un lot de messages échus. Renvoie le nombre de messages traités.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InternTrackDbContext>();
        var transport = scope.ServiceProvider.GetRequiredService<IEmailTransport>();

        var messages = await ClaimDueMessagesAsync(context, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await transport.SendAsync(message.ToEmail, message.Subject, message.HtmlBody, cancellationToken);
                message.MarkSent(_timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                message.MarkAttemptFailed($"{ex.GetType().Name}: {ex.Message}", _timeProvider.GetUtcNow().UtcDateTime);

                if (message.Status == EmailOutboxStatus.Failed)
                    _logger.LogError(ex, "E-mail {MessageId} abandonné après {Attempts} essais.", message.Id, message.Attempts);
                else
                    _logger.LogWarning("Échec d'envoi de l'e-mail {MessageId} (essai {Attempts}) : {Error}. Prochain essai à {NextAttemptAt:o}.",
                        message.Id, message.Attempts, ex.Message, message.NextAttemptAt);
            }

            // Enregistré message par message : un arrêt brutal ne provoque pas de renvoi des e-mails déjà partis.
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return messages.Count;
    }

    private static async Task<List<EmailOutboxMessage>> ClaimDueMessagesAsync(
        InternTrackDbContext context, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!context.Database.IsNpgsql())
        {
            // Base en mémoire (tests) : une seule instance, pas de verrouillage nécessaire.
            return await context.EmailOutbox
                .Where(m => m.Status == EmailOutboxStatus.Pending && m.NextAttemptAt <= nowUtc
                            && (m.LockedUntil == null || m.LockedUntil < nowUtc))
                .OrderBy(m => m.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        var ids = new List<Guid>();
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE "EmailOutbox" SET "LockedUntil" = @lockedUntil
                WHERE "Id" IN (
                    SELECT "Id" FROM "EmailOutbox"
                    WHERE "Status" = 0 AND "NextAttemptAt" <= @now
                      AND ("LockedUntil" IS NULL OR "LockedUntil" < @now)
                    ORDER BY "CreatedAt"
                    LIMIT @batch
                    FOR UPDATE SKIP LOCKED)
                RETURNING "Id";
                """;
            AddParameter(command, "lockedUntil", nowUtc + LockDuration);
            AddParameter(command, "now", nowUtc);
            AddParameter(command, "batch", BatchSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetGuid(0));
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        if (ids.Count == 0)
            return new List<EmailOutboxMessage>();

        return await context.EmailOutbox
            .Where(m => ids.Contains(m.Id))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>Supprime les messages envoyés depuis plus de 30 jours et les échecs de plus de 90 jours.</summary>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InternTrackDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var sentBefore = now - SentRetention;
        var failedBefore = now - FailedRetention;

        var query = context.EmailOutbox.Where(m =>
            (m.Status == EmailOutboxStatus.Sent && m.SentAt < sentBefore)
            || (m.Status == EmailOutboxStatus.Failed && m.CreatedAt < failedBefore));

        if (context.Database.IsRelational())
            return await query.ExecuteDeleteAsync(cancellationToken);

        var stale = await query.ToListAsync(cancellationToken);
        context.EmailOutbox.RemoveRange(stale);
        await context.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
