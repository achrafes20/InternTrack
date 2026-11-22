using InternTrack.Domain.Common;

namespace InternTrack.Domain.Events;

public record ProgressReportSubmittedEvent(
    Guid ReportId,
    Guid InternshipId,
    int WeekNumber,
    DateTime OccurredOn) : IDomainEvent;

