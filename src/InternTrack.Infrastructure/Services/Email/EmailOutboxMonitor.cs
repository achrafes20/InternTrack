using Microsoft.EntityFrameworkCore;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Domain.Common;
using InternTrack.Infrastructure.Persistence;
using InternTrack.Infrastructure.Persistence.Outbox;

namespace InternTrack.Infrastructure.Services.Email;

public class EmailOutboxMonitor : IEmailOutboxMonitor
{
    private readonly InternTrackDbContext _context;
    private readonly EmailOutboxSignal _signal;
    private readonly TimeProvider _timeProvider;

    public EmailOutboxMonitor(InternTrackDbContext context, EmailOutboxSignal signal, TimeProvider timeProvider)
    {
        _context = context;
        _signal = signal;
        _timeProvider = timeProvider;
    }

    public async Task<EmailOutboxSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.EmailOutbox.AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var oldestPending = await _context.EmailOutbox.AsNoTracking()
            .Where(m => m.Status == EmailOutboxStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Select(m => (DateTime?)m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        int CountOf(EmailOutboxStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;
        return new EmailOutboxSummaryDto(
            CountOf(EmailOutboxStatus.Pending), CountOf(EmailOutboxStatus.Sent), CountOf(EmailOutboxStatus.Failed), oldestPending);
    }

    public async Task<IReadOnlyList<EmailOutboxItemDto>> GetRecentAsync(string? status, int take, CancellationToken cancellationToken)
    {
        var query = _context.EmailOutbox.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EmailOutboxStatus>(status, ignoreCase: true, out var parsed))
            query = query.Where(m => m.Status == parsed);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        return items.Select(m => new EmailOutboxItemDto(
            m.Id, m.ToEmail, m.Subject, m.Status.ToString(), m.Attempts,
            m.CreatedAt, m.NextAttemptAt, m.SentAt, m.LastError)).ToList();
    }

    public async Task<Result> RequeueAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await _context.EmailOutbox.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (message == null)
            return Result.Failure("E-mail introuvable.");
        if (message.Status != EmailOutboxStatus.Failed)
            return Result.Failure("Seul un e-mail en échec peut être remis dans la file.");

        message.Requeue(_timeProvider.GetUtcNow().UtcDateTime);
        await _context.SaveChangesAsync(cancellationToken);
        _signal.Notify();
        return Result.Success();
    }
}
