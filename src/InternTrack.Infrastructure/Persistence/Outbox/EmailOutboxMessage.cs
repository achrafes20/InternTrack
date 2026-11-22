namespace InternTrack.Infrastructure.Persistence.Outbox;

public enum EmailOutboxStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

/// <summary>
/// E-mail en attente d'envoi (pattern « transactional outbox »). Il est écrit dans la même transaction que
/// la modification métier qui l'a produit, puis envoyé en arrière-plan par <c>EmailOutboxProcessor</c>
/// avec des essais espacés : une panne SMTP retarde l'e-mail, elle ne le perd plus.
/// </summary>
public class EmailOutboxMessage
{
    public const int MaxAttempts = 8;

    public Guid Id { get; private set; }
    public string ToEmail { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Corps HTML. Effacé après l'envoi : il peut contenir un lien personnel (réinitialisation, signature).</summary>
    public string HtmlBody { get; private set; } = string.Empty;

    public EmailOutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }

    /// <summary>Réservation par une instance de l'API : évite qu'un message soit envoyé deux fois en parallèle.</summary>
    public DateTime? LockedUntil { get; private set; }

    public DateTime? SentAt { get; private set; }
    public string? LastError { get; private set; }

    private EmailOutboxMessage() { }

    public static EmailOutboxMessage Create(string toEmail, string subject, string htmlBody, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        ToEmail = toEmail,
        Subject = subject,
        HtmlBody = htmlBody,
        Status = EmailOutboxStatus.Pending,
        CreatedAt = nowUtc,
        NextAttemptAt = nowUtc
    };

    public void MarkSent(DateTime nowUtc)
    {
        Status = EmailOutboxStatus.Sent;
        Attempts++;
        SentAt = nowUtc;
        LockedUntil = null;
        LastError = null;
        HtmlBody = string.Empty;
    }

    /// <summary>Échec d'un essai : nouvel essai après un délai exponentiel (30 s, 1 min, 2 min… plafonné à 1 h).</summary>
    public void MarkAttemptFailed(string error, DateTime nowUtc)
    {
        Attempts++;
        LockedUntil = null;
        LastError = error.Length > 1000 ? error[..1000] : error;

        if (Attempts >= MaxAttempts)
        {
            Status = EmailOutboxStatus.Failed;
            return;
        }

        NextAttemptAt = nowUtc + RetryDelay(Attempts);
    }

    public static TimeSpan RetryDelay(int attempts)
    {
        var seconds = 30 * Math.Pow(2, Math.Max(0, attempts - 1));
        return TimeSpan.FromSeconds(Math.Min(seconds, 3600));
    }

    /// <summary>Remet un message abandonné dans la file (action d'administration).</summary>
    public void Requeue(DateTime nowUtc)
    {
        if (Status != EmailOutboxStatus.Failed) return;
        Status = EmailOutboxStatus.Pending;
        Attempts = 0;
        NextAttemptAt = nowUtc;
        LockedUntil = null;
    }
}
