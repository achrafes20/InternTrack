using Microsoft.EntityFrameworkCore;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Common.Security;
using InternTrack.Application.Features.Internships.DTOs;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;

namespace InternTrack.Application.Features.Documents;

public record DocumentContent(Stream Content, string FileName, string ContentType, Guid InternshipId);

public interface IDocumentService
{
    Task<List<InternshipDocumentDto>> ListAsync(Guid internshipId, CancellationToken cancellationToken);

    /// <summary>
    /// Dépose un document PDF. Un nouveau dépôt du même type remplace le précédent
    /// (le fichier remplacé est supprimé du stockage).
    /// </summary>
    Task<Result<InternshipDocumentDto>> UploadAsync(
        Guid internshipId,
        InternshipDocumentKind kind,
        string fileName,
        long declaredLength,
        Stream content,
        string uploadedBy,
        CancellationToken cancellationToken);

    Task<Guid?> GetInternshipIdAsync(Guid documentId, CancellationToken cancellationToken);
    Task<Result<DocumentContent>> OpenAsync(Guid documentId, CancellationToken cancellationToken);
}

public class DocumentService : IDocumentService
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private readonly IApplicationDbContext _context;
    private readonly IFileStorage _storage;
    private readonly IMalwareScanner? _malwareScanner;

    public DocumentService(IApplicationDbContext context, IFileStorage storage, IMalwareScanner? malwareScanner = null)
    {
        _context = context;
        _storage = storage;
        _malwareScanner = malwareScanner;
    }

    public async Task<List<InternshipDocumentDto>> ListAsync(Guid internshipId, CancellationToken cancellationToken)
        => await _context.InternshipDocuments.AsNoTracking()
            .Where(d => d.InternshipId == internshipId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new InternshipDocumentDto(d.Id, d.Kind.ToString(), d.FileName, d.SizeBytes, d.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<Result<InternshipDocumentDto>> UploadAsync(
        Guid internshipId,
        InternshipDocumentKind kind,
        string fileName,
        long declaredLength,
        Stream content,
        string uploadedBy,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
            return Result<InternshipDocumentDto>.Failure("Type de document inconnu.");

        if (declaredLength <= 0)
            return Result<InternshipDocumentDto>.Failure("Le fichier est vide.");

        if (declaredLength > InternshipDocument.MaxSizeBytes)
            return Result<InternshipDocumentDto>.Failure($"Le fichier dépasse la taille maximale de {InternshipDocument.MaxSizeBytes / (1024 * 1024)} Mo.");

        var internship = await _context.Internships
            .Include(i => i.Defense)
            .Include(i => i.Convention)
            .FirstOrDefaultAsync(i => i.Id == internshipId, cancellationToken);

        if (internship == null)
            return Result<InternshipDocumentDto>.Failure("Stage introuvable.");

        var rule = CheckUploadRules(internship, kind);
        if (rule.IsFailure)
            return Result<InternshipDocumentDto>.Failure(rule.Error);

        // Lecture bornée en mémoire : la taille réelle et la signature PDF sont vérifiées
        // (on ne fait confiance ni à l'extension ni au Content-Type envoyés par le navigateur).
        using var buffer = new MemoryStream();
        var copied = await CopyBoundedAsync(content, buffer, InternshipDocument.MaxSizeBytes, cancellationToken);
        if (copied < 0)
            return Result<InternshipDocumentDto>.Failure($"Le fichier dépasse la taille maximale de {InternshipDocument.MaxSizeBytes / (1024 * 1024)} Mo.");

        if (!StartsWithPdfSignature(buffer))
            return Result<InternshipDocumentDto>.Failure("Seuls les fichiers PDF sont acceptés.");

        // Contenu actif (JavaScript, lancement, pièces jointes…) : refusé quel que soit l'antivirus.
        var bytes = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        var inspection = PdfSafetyInspector.Inspect(bytes.Span);
        if (!inspection.IsSafe)
            return Result<InternshipDocumentDto>.Failure(inspection.Reason!);

        if (_malwareScanner is { IsEnabled: true })
        {
            var scan = await _malwareScanner.ScanAsync(bytes, cancellationToken);
            if (scan.Status == MalwareScanStatus.Infected)
                return Result<InternshipDocumentDto>.Failure("Le fichier a été détecté comme malveillant par l'antivirus et a été refusé.");
            if (scan.Status == MalwareScanStatus.Unavailable)
                return Result<InternshipDocumentDto>.Failure("L'analyse antivirus est momentanément indisponible. Réessayez dans quelques minutes.");
        }

        buffer.Position = 0;
        var storageKey = await _storage.SaveAsync(buffer, "pdf", cancellationToken);

        var previous = await _context.InternshipDocuments
            .Where(d => d.InternshipId == internshipId && d.Kind == kind)
            .ToListAsync(cancellationToken);

        var document = new InternshipDocument(internshipId, kind, fileName, copied, storageKey, uploadedBy);
        _context.InternshipDocuments.RemoveRange(previous);
        await _context.InternshipDocuments.AddAsync(document, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // La base n'a pas enregistré le document : le fichier écrit ne doit pas rester orphelin.
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        foreach (var old in previous)
            await _storage.DeleteAsync(old.StorageKey, CancellationToken.None);

        return Result<InternshipDocumentDto>.Success(
            new InternshipDocumentDto(document.Id, document.Kind.ToString(), document.FileName, document.SizeBytes, document.CreatedAt));
    }

    public Task<Guid?> GetInternshipIdAsync(Guid documentId, CancellationToken cancellationToken)
        => _context.InternshipDocuments.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => (Guid?)d.InternshipId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Result<DocumentContent>> OpenAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _context.InternshipDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
            return Result<DocumentContent>.Failure("Document introuvable.");

        var stream = await _storage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (stream == null)
            return Result<DocumentContent>.Failure("Le fichier n'est plus disponible sur le serveur.");

        return Result<DocumentContent>.Success(new DocumentContent(stream, document.FileName, document.ContentType, document.InternshipId));
    }

    private static Result CheckUploadRules(Internship internship, InternshipDocumentKind kind)
    {
        switch (kind)
        {
            case InternshipDocumentKind.FinalThesis:
                if (!Internship.RunningStatuses.Contains(internship.Status))
                    return Result.Failure("Le mémoire ne peut être déposé que pendant le stage (en cours, soutenance planifiée ou ajourné).");
                // Après un ajournement, l'élève dépose un mémoire révisé pour la session de rattrapage.
                if (internship.Defense?.FinalGrade != null && internship.Status != InternshipStatus.Ajourne)
                    return Result.Failure("La soutenance a déjà été notée : le mémoire ne peut plus être modifié.");
                return Result.Success();

            case InternshipDocumentKind.SignedConvention:
                if (internship.Convention == null)
                    return Result.Failure("Aucune convention n'existe encore pour ce stage.");
                if (internship.Status is InternshipStatus.Brouillon or InternshipStatus.Propose or InternshipStatus.Refuse or InternshipStatus.Termine or InternshipStatus.NonValide or InternshipStatus.Annule)
                    return Result.Failure("Le scan de la convention ne peut être déposé que pour un dossier validé ou en cours.");
                return Result.Success();

            default:
                return Result.Failure("Type de document inconnu.");
        }
    }

    /// <summary>Copie au plus <paramref name="maxBytes"/> octets ; renvoie -1 si la source est plus grande.</summary>
    private static async Task<long> CopyBoundedAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes) return -1;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return total;
    }

    private static bool StartsWithPdfSignature(MemoryStream stream)
    {
        if (stream.Length < PdfMagic.Length) return false;
        var span = stream.GetBuffer().AsSpan(0, PdfMagic.Length);
        return span.SequenceEqual(PdfMagic);
    }
}
