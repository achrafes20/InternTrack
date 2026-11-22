namespace InternTrack.Domain.Common;

/// <summary>
/// Longueurs maximales des champs persistés. Source unique de vérité partagée par la configuration
/// EF Core (taille des colonnes) et par les validateurs d'entrée : une donnée trop longue est refusée
/// en 400 avant d'atteindre la base, au lieu de provoquer une erreur 500.
/// </summary>
public static class FieldLimits
{
    public const int UserId = 450;
    public const int PersonName = 150;
    public const int FirstOrLastName = 70;
    public const int Email = 150;
    public const int Phone = 30;
    public const int StudentNumber = 20;
    public const int Password = 128;

    public const int InternshipTitle = 250;
    public const int LongDescription = 4000;
    public const int RejectionReason = 1000;

    public const int CompanyName = 200;
    public const int Industry = 100;
    public const int City = 100;
    public const int Address = 300;
    public const int Website = 200;
    public const int Country = 100;

    public const int RequiredSkills = 500;

    public const int ReportTitle = 200;
    public const int ReportActivities = 5000;
    public const int ReportSection = 2000;
    public const int AiSummary = 3000;
    public const int Comment = 2000;

    public const int Classroom = 50;
    public const int DefenseComments = 2000;

    public const int TrackCode = 20;
    public const int TrackName = 150;
    public const int TrackDescription = 500;

    public const int ConventionNumber = 50;
    public const int VerificationToken = 64;
    public const int TokenHash = 64;
    public const int Sha256 = 64;
    public const int AcademicYear = 9;

    public const int FileName = 200;
    public const int StorageKey = 300;
    public const int ContentType = 100;

    /// <summary>Tronque une valeur produite par le système (ex. sortie d'IA) à la taille de sa colonne.</summary>
    public static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
