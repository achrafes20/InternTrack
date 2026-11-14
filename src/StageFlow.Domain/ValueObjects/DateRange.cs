namespace StageFlow.Domain.ValueObjects;

public record DateRange(DateTime StartDate, DateTime EndDate)
{
    public static (bool IsValid, string? Error) Validate(DateTime start, DateTime end)
    {
        if (end <= start)
            return (false, "La date de fin doit être postérieure à la date de début.");

        return (true, null);
    }

    public bool OverlapsWith(DateRange other)
    {
        return StartDate < other.EndDate && other.StartDate < EndDate;
    }
}

