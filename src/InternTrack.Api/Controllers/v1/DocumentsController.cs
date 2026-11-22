using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternTrack.Application.Common.Security;
using InternTrack.Application.Features.Documents;
using InternTrack.Domain.Entities;

namespace InternTrack.Api.Controllers.v1;

/// <summary>Documents rattachés aux stages : mémoire final et scan de la convention signée (PDF uniquement).</summary>
[Authorize]
[ApiController]
[Route("api/v1")]
public class DocumentsController : ControllerBase
{
    // Marge pour l'enveloppe multipart autour d'un fichier de 20 Mo.
    private const long MaxRequestBytes = InternshipDocument.MaxSizeBytes + 1024 * 1024;

    private readonly IDocumentService _documentService;
    private readonly IAccessControlService _accessControl;

    public DocumentsController(IDocumentService documentService, IAccessControlService accessControl)
    {
        _documentService = documentService;
        _accessControl = accessControl;
    }

    [HttpGet("internships/{internshipId:guid}/documents")]
    public async Task<IActionResult> List(Guid internshipId, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanViewInternshipDocumentsAsync(internshipId, cancellationToken))
            return Forbid();

        return Ok(await _documentService.ListAsync(internshipId, cancellationToken));
    }

    /// <summary>Dépôt du mémoire de fin d'études par l'élève (remplace le dépôt précédent jusqu'à la notation).</summary>
    [HttpPost("internships/{internshipId:guid}/documents/thesis")]
    [Authorize(Policy = "RequireStudentRole")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> UploadThesis(Guid internshipId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken))
            return Forbid();

        return await UploadAsync(internshipId, InternshipDocumentKind.FinalThesis, file, cancellationToken);
    }

    /// <summary>Dépôt du scan de la convention signée par l'entreprise (élève du stage ou responsable de la filière).</summary>
    [HttpPost("internships/{internshipId:guid}/documents/signed-convention")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> UploadSignedConvention(Guid internshipId, IFormFile? file, CancellationToken cancellationToken)
    {
        var allowed = _accessControl.IsStudent
            ? await _accessControl.CanAccessInternshipAsync(internshipId, cancellationToken)
            : await _accessControl.CanManageInternshipAsync(internshipId, cancellationToken);
        if (!allowed)
            return Forbid();

        return await UploadAsync(internshipId, InternshipDocumentKind.SignedConvention, file, cancellationToken);
    }

    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        var internshipId = await _documentService.GetInternshipIdAsync(documentId, cancellationToken);
        if (internshipId == null)
            return NotFound(new ProblemDetails { Title = "Document introuvable", Status = 404 });

        if (!await _accessControl.CanViewInternshipDocumentsAsync(internshipId.Value, cancellationToken))
            return Forbid();

        var result = await _documentService.OpenAsync(documentId, cancellationToken);
        if (result.IsFailure)
            return NotFound(new ProblemDetails { Title = "Document indisponible", Detail = result.Error, Status = 404 });

        var document = result.Value!;
        // Content-Disposition « attachment » : le PDF est téléchargé, jamais interprété dans l'origine de l'API.
        return File(document.Content, document.ContentType, document.FileName);
    }

    private async Task<IActionResult> UploadAsync(Guid internshipId, InternshipDocumentKind kind, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Fichier manquant", Detail = "Sélectionnez un fichier PDF à déposer.", Status = 400 });

        await using var stream = file.OpenReadStream();
        var result = await _documentService.UploadAsync(internshipId, kind, file.FileName, file.Length, stream, _accessControl.UserId!, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Dépôt impossible", Detail = result.Error, Status = 400 });

        return Ok(result.Value);
    }
}
