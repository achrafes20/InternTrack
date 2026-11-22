using Microsoft.Extensions.Configuration;

namespace InternTrack.Application.Features.Internships;

/// <summary>
/// Règles réglementaires de durée des stages, configurables par établissement (section « Internships »).
/// </summary>
public class InternshipPolicy
{
    /// <summary>Durée minimale d'un stage PFE (8 semaines par défaut).</summary>
    public int MinDurationWeeks { get; init; } = 8;

    /// <summary>Durée maximale (52 semaines par défaut).</summary>
    public int MaxDurationWeeks { get; init; } = 52;

    /// <summary>Écart toléré entre la durée du stage et celle de l'offre à laquelle il répond.</summary>
    public int OfferDurationToleranceWeeks { get; init; } = 2;

    /// <summary>Validité du lien de signature envoyé à l'entreprise.</summary>
    public int CompanySigningLinkValidityDays { get; init; } = 14;

    public static InternshipPolicy FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Internships");
        var defaults = new InternshipPolicy();
        return new InternshipPolicy
        {
            MinDurationWeeks = section.GetValue("MinDurationWeeks", defaults.MinDurationWeeks),
            MaxDurationWeeks = section.GetValue("MaxDurationWeeks", defaults.MaxDurationWeeks),
            OfferDurationToleranceWeeks = section.GetValue("OfferDurationToleranceWeeks", defaults.OfferDurationToleranceWeeks),
            CompanySigningLinkValidityDays = section.GetValue("CompanySigningLinkValidityDays", defaults.CompanySigningLinkValidityDays),
        };
    }

    public static int DurationInWeeks(DateTime start, DateTime end) => (int)Math.Round((end - start).TotalDays / 7.0);
}
