using InternTrack.Domain.Common;

namespace InternTrack.Domain.Entities;

public class AcademicTrack : BaseAuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    // Relations
    public ICollection<InternshipOffer> Offers { get; private set; } = new List<InternshipOffer>();
    public ICollection<Internship> Internships { get; private set; } = new List<Internship>();

    private AcademicTrack() { }

    public AcademicTrack(string code, string name, string description)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
    }

    public void Update(string name, string description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
    }
}

