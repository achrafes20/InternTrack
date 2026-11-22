using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternTrack.Application.Common.Interfaces;

namespace InternTrack.Api.Controllers.v1;

public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role,
    string? StudentNumber,
    Guid? AcademicTrackId,
    int? GraduationYear = null);

public record UpdateRoleRequest(string Role, Guid? AcademicTrackId, int? GraduationYear = null);

public record SetUserActiveRequest(bool IsActive);

[Authorize(Policy = "RequireAdminRole")]
public class UsersController : ApiControllerBase
{
    private readonly IIdentityService _identityService;

    public UsersController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var result = await _identityService.GetAllUsersAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateUserAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.Role,
            request.StudentNumber,
            request.AcademicTrackId,
            request.GraduationYear,
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Erreur de création", Detail = result.Error, Status = 400 });

        return CreatedAtAction(nameof(GetAllUsers), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _identityService.UpdateUserRoleAsync(id, request.Role, request.AcademicTrackId, request.GraduationYear, currentUserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Erreur de mise à jour", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Désactive un compte (départ, fin de contrat…) ou le réactive. L'historique est conservé.</summary>
    [HttpPut("{id}/active")]
    public async Task<IActionResult> SetActive(string id, [FromBody] SetUserActiveRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _identityService.SetUserActiveAsync(id, request.IsActive, currentUserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Opération impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>Envoie à l'utilisateur un lien de réinitialisation de son mot de passe.</summary>
    [HttpPost("{id}/password-reset")]
    public async Task<IActionResult> SendPasswordReset(string id, CancellationToken cancellationToken)
    {
        var user = await _identityService.GetUserByIdAsync(id, cancellationToken);
        if (user.IsFailure)
            return NotFound(new ProblemDetails { Title = "Utilisateur introuvable", Status = 404 });

        if (!user.Value!.IsActive)
            return BadRequest(new ProblemDetails { Title = "Compte inactif", Detail = "Réactivez le compte avant d'envoyer un lien de réinitialisation.", Status = 400 });

        await _identityService.RequestPasswordResetAsync(user.Value.Email, cancellationToken);
        return NoContent();
    }

    /// <summary>Déverrouille un compte bloqué après des échecs de connexion répétés.</summary>
    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
    {
        var result = await _identityService.UnlockUserAsync(id, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Déverrouillage impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }
}

