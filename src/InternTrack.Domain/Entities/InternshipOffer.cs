using InternTrack.Domain.Common;

namespace InternTrack.Domain.Entities;

public class InternshipOffer : BaseAuditableEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string RequiredSkills { get; private set; } = string.Empty; // e.g. "C#, React, Docker, EF Core"
    public string City { get; private set; } = string.Empty;
    public int DurationInWeeks { get; private set; }
    public decimal? MonthlyStipend { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime ExpirationDate { get; private set; }

    // Foreign Keys
    public Guid CompanyId { get; private set; }
    public Company Company { get; private set; } = null!;

    public Guid AcademicTrackId { get; private set; }
    public AcademicTrack AcademicTrack { get; private set; } = null!;

    public ICollection<Internship> Applications { get; private set; } = new List<Internship>();

    private InternshipOffer() { }

    public InternshipOffer(
        string title,
        string description,
        string requiredSkills,
        string city,
        int durationInWeeks,
        decimal? monthlyStipend,
        DateTime expirationDate,
        Guid companyId,
        Guid academicTrackId)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description ?? string.Empty;
        RequiredSkills = requiredSkills ?? string.Empty;
        City = city ?? throw new ArgumentNullException(nameof(city));
        DurationInWeeks = durationInWeeks > 0 ? durationInWeeks : throw new ArgumentException("La durée doit être positive.", nameof(durationInWeeks));
        MonthlyStipend = monthlyStipend;
        ExpirationDate = expirationDate;
        CompanyId = companyId;
        AcademicTrackId = academicTrackId;
        IsActive = true;
    }

    public void Update(
        string title,
        string description,
        string requiredSkills,
        string city,
        int durationInWeeks,
        decimal? monthlyStipend,
        DateTime expirationDate,
        bool isActive)
    {
        Title = title;
        Description = description;
        RequiredSkills = requiredSkills;
        City = city;
        DurationInWeeks = durationInWeeks;
        MonthlyStipend = monthlyStipend;
        ExpirationDate = expirationDate;
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

