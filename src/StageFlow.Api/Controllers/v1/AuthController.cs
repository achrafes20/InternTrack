using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Api.Controllers.v1;

public record LoginRequest(string Email, string Password);

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
