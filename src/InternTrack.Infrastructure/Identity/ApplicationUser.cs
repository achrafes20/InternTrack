using Microsoft.AspNetCore.Identity;

namespace InternTrack.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? StudentNumber { get; set; } // Apogée / Code étudiant ENSA
    public Guid? AcademicTrackId { get; set; } // Filière rattachée

    /// <summary>Promotion de l'élève : année de sortie prévue (ex. 2027 pour la « promotion 2027 »).</summary>
    public int? GraduationYear { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

