using StageFlow.Domain.Common;

namespace StageFlow.Domain.Events;

public record ProgressReportSubmittedEvent(
    Guid ReportId,
    Guid InternshipId,
    int WeekNumber,
    DateTime OccurredOn) : IDomainEvent;

