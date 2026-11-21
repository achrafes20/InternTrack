using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Api.Controllers.v1;

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public class AuthController : ApiControllerBase
{
    public const string RefreshCookieName = "stageflow_refresh_token";

    private readonly IIdentityService _identityService;
    private readonly IConfiguration _configuration;

    public AuthController(IIdentityService identityService, IConfiguration configuration)
    {
        _identityService = identityService;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login_limiter")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new ProblemDetails { Title = "Requête invalide", Detail = "L'email et le mot de passe sont obligatoires.", Status = 400 });

        var result = await _identityService.AuthenticateAsync(request.Email.Trim(), request.Password, GetIpAddress(), cancellationToken);

        if (result.IsFailure)
            return Unauthorized(new ProblemDetails { Title = "Échec d'authentification", Detail = result.Error, Status = 401 });

        // Refresh token sécurisé dans un cookie HttpOnly
        SetRefreshTokenCookie(result.Value!.RefreshToken, result.Value.RefreshTokenExpiresAt);

        return Ok(new
        {
            accessToken = result.Value.AccessToken,
            accessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            user = result.Value.User
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh_limiter")]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new ProblemDetails { Title = "Non autorisé", Detail = "Cookie de rafraîchissement absent.", Status = 401 });

        var result = await _identityService.RefreshTokenAsync(refreshToken, GetIpAddress(), cancellationToken);

        if (result.IsFailure)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized(new ProblemDetails { Title = "Session expirée", Detail = result.Error, Status = 401 });
        }

        SetRefreshTokenCookie(result.Value!.RefreshToken, result.Value.RefreshTokenExpiresAt);

        return Ok(new
        {
            accessToken = result.Value.AccessToken,
            accessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            user = result.Value.User
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _identityService.RevokeTokenAsync(refreshToken, GetIpAddress(), cancellationToken);
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    /// <summary>Changement de mot de passe par l'utilisateur connecté. Les autres sessions sont fermées.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _identityService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Changement impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    /// <summary>
    /// Demande de réinitialisation. La réponse est toujours 204 : elle ne révèle pas si l'email correspond à un compte.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("public_token_limiter")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _identityService.RequestPasswordResetAsync(request.Email.Trim(), cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("public_token_limiter")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.ResetPasswordAsync(request.Email.Trim(), request.Token, request.NewPassword, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Réinitialisation impossible", Detail = result.Error, Status = 400 });

        return NoContent();
    }

    private CookieOptions BuildCookieOptions(DateTime? expires = null)
    {
        // Front et API sur des domaines différents (ex. Azure Static Web Apps + App Service) :
        // le cookie doit être SameSite=None, ce qui impose Secure (HTTPS).
        var crossSite = _configuration.GetValue<bool>("Auth:CrossSiteRefreshCookie");

        return new CookieOptions
        {
            HttpOnly = true,
            Expires = expires,
            // En développement local (HTTP), le navigateur refuserait un cookie Secure.
            Secure = crossSite || Request.IsHttps,
            SameSite = crossSite ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/api/v1/auth"
        };
    }

    private void SetRefreshTokenCookie(string token, DateTime expires)
        => Response.Cookies.Append(RefreshCookieName, token, BuildCookieOptions(expires));

    private void DeleteRefreshTokenCookie()
        => Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions());
}
