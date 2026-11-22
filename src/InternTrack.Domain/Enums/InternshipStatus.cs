namespace InternTrack.Domain.Enums;

public enum InternshipStatus
{
    Brouillon = 1,
    Propose = 2,
    Valide = 3,
    Refuse = 4,
    EnCours = 5,
    SoutenancePlanifiee = 6,

    /// <summary>Soutenance réussie (note finale supérieure ou égale au seuil de validation). État final.</summary>
    Termine = 7,

    /// <summary>Dossier annulé administrativement (abandon, doublon, données incohérentes). État final.</summary>
    Annule = 8,

    /// <summary>Soutenance non validée (note inférieure au seuil) : une session de rattrapage peut être planifiée.</summary>
    Ajourne = 9,

    /// <summary>Soutenance non validée après épuisement des sessions autorisées. État final.</summary>
    NonValide = 10
}
