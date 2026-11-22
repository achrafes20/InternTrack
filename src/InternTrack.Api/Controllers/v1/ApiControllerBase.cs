namespace InternTrack.Api.Controllers.v1;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// IP du client. Elle est déjà corrigée par le middleware ForwardedHeaders lorsque la requête
    /// provient d'un proxy de confiance ; l'en-tête X-Forwarded-For brut n'est jamais lu ici (falsifiable).
    /// </summary>
    protected string? GetIpAddress()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        if (address == null) return null;
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    }
}
