namespace StageFlow.Domain.Enums;

public enum InternshipStatus
{
    Brouillon = 1,
    Propose = 2,
    Valide = 3,
    Refuse = 4,
    EnCours = 5,
    SoutenancePlanifiee = 6,
    Termine = 7,

    /// <summary>Dossier annulé administrativement (abandon, doublon, données incohérentes). État final.</summary>
    Annule = 8
}

