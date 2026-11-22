using InternTrack.Domain.Common;

namespace InternTrack.Domain.Events;

public record ConventionCompanySignedEvent(
    Guid ConventionId,
    Guid InternshipId,
    string? SignerName,
    DateTime OccurredOn) : IDomainEvent;

public record ConventionFullySignedEvent(
    Guid ConventionId,
    Guid InternshipId,
    DateTime OccurredOn) : IDomainEvent;

public record ReportCommentAddedEvent(
    Guid CommentId,
    Guid ProgressReportId,
    string AuthorId,
    DateTime OccurredOn) : IDomainEvent;
