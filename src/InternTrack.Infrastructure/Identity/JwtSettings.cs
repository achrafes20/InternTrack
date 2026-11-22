namespace InternTrack.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>Délai pendant lequel un refresh token qui vient d'être remplacé reste accepté (plusieurs onglets).</summary>
    public int RefreshTokenReuseGraceSeconds { get; set; } = 30;
}

