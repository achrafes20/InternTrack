using StageFlow.Domain.Common;

namespace StageFlow.Domain.Entities;

public class Convention : BaseAuditableEntity
{
    public string ConventionNumber { get; private set; } = string.Empty;
    public string VerificationToken { get; private set; } = string.Empty;
    public bool IsSignedByStudent { get; private set; }
    public bool IsSignedByCompany { get; private set; }
    public bool IsSignedBySchool { get; private set; }
    public DateTime? SignedAt { get; private set; }

    // Foreign Key 1-to-1
    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    private Convention() { }

    public Convention(Guid internshipId, string conventionNumber)
    {
        InternshipId = internshipId;
        ConventionNumber = conventionNumber ?? throw new ArgumentNullException(nameof(conventionNumber));
        VerificationToken = Guid.NewGuid().ToString("N");
    }

    public void SignByStudent()
    {
        IsSignedByStudent = true;
        CheckCompletion();
    }

    public void SignByCompany()
    {
        IsSignedByCompany = true;
        CheckCompletion();
    }

    public void SignBySchool()
    {
        IsSignedBySchool = true;
        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (IsSignedByStudent && IsSignedByCompany && IsSignedBySchool && SignedAt == null)
        {
            SignedAt = DateTime.UtcNow;
        }
    }
}

