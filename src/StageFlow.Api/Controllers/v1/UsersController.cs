using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Api.Controllers.v1;

public record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role,
    string? StudentNumber,
    Guid? AcademicTrackId);

public record UpdateRoleRequest(string Role, Guid? AcademicTrackId);

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
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Erreur de création", Detail = result.Error, Status = 400 });

        return CreatedAtAction(nameof(GetAllUsers), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _identityService.UpdateUserRoleAsync(id, request.Role, request.AcademicTrackId, currentUserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Erreur de mise à jour", Detail = result.Error, Status = 400 });

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

