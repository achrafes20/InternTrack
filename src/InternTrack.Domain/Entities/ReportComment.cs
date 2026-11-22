using InternTrack.Domain.Common;

namespace InternTrack.Domain.Entities;

public class ReportComment : BaseAuditableEntity
{
    public string Content { get; private set; } = string.Empty;
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorFullName { get; private set; } = string.Empty;

    public Guid ProgressReportId { get; private set; }
    public ProgressReport ProgressReport { get; private set; } = null!;

    private ReportComment() { }

    public ReportComment(Guid progressReportId, string authorId, string authorFullName, string content)
    {
        ProgressReportId = progressReportId;
        AuthorId = authorId ?? throw new ArgumentNullException(nameof(authorId));
        AuthorFullName = FieldLimits.Truncate(authorFullName ?? throw new ArgumentNullException(nameof(authorFullName)), FieldLimits.PersonName);
        Content = content ?? throw new ArgumentNullException(nameof(content));

        AddDomainEvent(new Events.ReportCommentAddedEvent(Id, progressReportId, authorId, DateTime.UtcNow));
    }
}

