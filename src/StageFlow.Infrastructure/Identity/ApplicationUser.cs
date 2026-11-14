using Microsoft.AspNetCore.Identity;

namespace StageFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? StudentNumber { get; set; } // Apogée / Code étudiant ENSA
    public Guid? AcademicTrackId { get; set; } // Filière rattachée
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

