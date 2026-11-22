using InternTrack.Domain.Common;

namespace InternTrack.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty; // Create, Update, StatusChange, DeleteAttempt
    public string? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
    public string? Changes { get; private set; } // JSON des propriétés modifiées
    public string? AdditionalMetadata { get; private set; }

    private AuditLog() { }

    public AuditLog(
        string entityName,
        string entityId,
        string action,
        string? userId,
        string? userEmail,
        string? changes = null,
        string? additionalMetadata = null)
    {
        EntityName = entityName ?? throw new ArgumentNullException(nameof(entityName));
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        Action = action ?? throw new ArgumentNullException(nameof(action));
        UserId = userId;
        UserEmail = userEmail;
        Changes = changes;
        AdditionalMetadata = additionalMetadata;
        Timestamp = DateTime.UtcNow;
    }
}

