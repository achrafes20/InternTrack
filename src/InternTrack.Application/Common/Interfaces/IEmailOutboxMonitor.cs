using InternTrack.Domain.Common;

namespace InternTrack.Application.Common.Interfaces;

public record EmailOutboxSummaryDto(int Pending, int Sent, int Failed, DateTime? OldestPendingAt);

public record EmailOutboxItemDto(
    Guid Id,
    string ToEmail,
    string Subject,
    string Status,
    int Attempts,
    DateTime CreatedAt,
    DateTime NextAttemptAt,
    DateTime? SentAt,
    string? LastError);

/// <summary>Supervision de la file d'e-mails (administration) : état, échecs et remise en file.</summary>
public interface IEmailOutboxMonitor
{
    Task<EmailOutboxSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailOutboxItemDto>> GetRecentAsync(string? status, int take, CancellationToken cancellationToken);
    Task<Result> RequeueAsync(Guid id, CancellationToken cancellationToken);
}
