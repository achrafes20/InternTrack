using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Api.Controllers.v1;

public record LoginRequest(string Email, string Password);

public class AuthController : ApiControllerBase
{
    private readonly IIdentityService _identityService;

    public AuthController(IIdentityService identityService)
    {
        _identityService = identityService;
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
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies["stageflow_refresh_token"];
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
        var refreshToken = Request.Cookies["stageflow_refresh_token"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _identityService.RevokeTokenAsync(refreshToken, GetIpAddress(), cancellationToken);
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    private void SetRefreshTokenCookie(string token, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = expires,
            // Le cookie n'est marqué Secure qu'en HTTPS : en développement local (HTTP),
            // le navigateur refuserait sinon de le stocker.
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax, // Permet les appels cross-port localhost en développement
            Path = "/api/v1/auth"
        };

        Response.Cookies.Append("stageflow_refresh_token", token, cookieOptions);
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete("stageflow_refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth"
        });
    }
}

