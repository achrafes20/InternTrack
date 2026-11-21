using StageFlow.Domain.Common;

namespace StageFlow.Domain.Entities;

public enum InternshipDocumentKind
{
    /// <summary>Scan de la convention signée sur papier par l'entreprise.</summary>
    SignedConvention = 1,

    /// <summary>Mémoire de fin d'études déposé par l'élève avant la soutenance.</summary>
    FinalThesis = 2
}

/// <summary>
/// Fichier rattaché à un dossier de stage. Le contenu est conservé par le service de stockage ;
/// l'entité n'en garde que la clé et les métadonnées.
/// </summary>
public class InternshipDocument : BaseAuditableEntity
{
    public const long MaxSizeBytes = 20 * 1024 * 1024;
    public const string PdfContentType = "application/pdf";

    public Guid InternshipId { get; private set; }
    public Internship Internship { get; private set; } = null!;

    public InternshipDocumentKind Kind { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = PdfContentType;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string UploadedBy { get; private set; } = string.Empty;

    private InternshipDocument() { }

    public InternshipDocument(Guid internshipId, InternshipDocumentKind kind, string fileName, long sizeBytes, string storageKey, string uploadedBy)
    {
        InternshipId = internshipId;
        Kind = kind;
        FileName = SanitizeFileName(fileName);
        SizeBytes = sizeBytes;
        StorageKey = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
        UploadedBy = uploadedBy ?? throw new ArgumentNullException(nameof(uploadedBy));
    }

    /// <summary>Nom d'affichage sûr : sans chemin ni caractères de contrôle, borné en longueur.</summary>
    public static string SanitizeFileName(string? fileName)
    {
        // Les navigateurs sous Windows peuvent envoyer « C:\...\fichier.pdf » : l'antislash n'est pas
        // un séparateur sous Linux, il est donc normalisé avant d'extraire le nom.
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '"' && c != '\\' && c != '/').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "document.pdf";
        if (!cleaned.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) cleaned += ".pdf";
        return cleaned.Length <= FieldLimits.FileName ? cleaned : cleaned[^FieldLimits.FileName..];
    }
}
