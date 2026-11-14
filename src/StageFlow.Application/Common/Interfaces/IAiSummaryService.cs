namespace StageFlow.Application.Common.Interfaces;

public interface IAiSummaryService
{
    Task<string> SummarizeReportAsync(string activities, string skills, string difficulties, string nextGoals, CancellationToken cancellationToken);
}

