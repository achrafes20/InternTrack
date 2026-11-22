using Microsoft.Extensions.Configuration;

namespace InternTrack.Application.Features.Defenses;

/// <summary>
/// Règlement des soutenances, configurable par établissement (section « Defenses »).
/// </summary>
public class DefensePolicy
{
    /// <summary>Note minimale pour valider le stage (10/20 par défaut).</summary>
    public decimal PassingGrade { get; init; } = 10m;

    /// <summary>Nombre total de sessions autorisées, rattrapage compris (2 par défaut : session normale + 1 rattrapage).</summary>
    public int MaxAttempts { get; init; } = 2;

    /// <summary>
    /// Part de la note du tuteur entreprise dans la note finale (0,25 par défaut) ; le reste revient à la moyenne
    /// des membres internes du jury. Sans évaluation du tuteur, la note finale est la moyenne du jury interne.
    /// </summary>
    public decimal TutorWeight { get; init; } = 0.25m;

    /// <summary>Exige l'évaluation du tuteur entreprise (quand il siège au jury) avant l'arrêt de la note.</summary>
    public bool RequireTutorEvaluation { get; init; }

    /// <summary>Une soutenance ne peut pas avoir lieu plus de N jours avant la fin du stage (30 par défaut).</summary>
    public int EarliestDaysBeforeEnd { get; init; } = 30;

    /// <summary>Une soutenance (session normale) doit avoir lieu au plus N jours après la fin du stage (90 par défaut).</summary>
    public int LatestDaysAfterEnd { get; init; } = 90;

    /// <summary>Un rattrapage doit avoir lieu au plus N jours après la fin du stage (180 par défaut).</summary>
    public int RetakeLatestDaysAfterEnd { get; init; } = 180;

    /// <summary>
    /// Proportion minimale de rapports hebdomadaires validés par l'encadrant, rapportée aux semaines écoulées
    /// (0,5 par défaut, arrondi inférieur ; 0 désactive la règle). Vérifiée à la planification et à l'arrêt de la note.
    /// </summary>
    public decimal MinValidatedReportRatio { get; init; } = 0.5m;

    public bool IsPassing(decimal grade) => grade >= PassingGrade;

    public bool CanRetakeAfter(int attempt) => attempt < MaxAttempts;

    /// <summary>Nombre de rapports validés exigés pour <paramref name="elapsedWeeks"/> semaines écoulées.</summary>
    public int RequiredValidatedReports(int elapsedWeeks)
        => (int)Math.Floor(Math.Max(0, elapsedWeeks) * MinValidatedReportRatio);

    public static DefensePolicy FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Defenses");
        var d = new DefensePolicy();
        var policy = new DefensePolicy
        {
            PassingGrade = section.GetValue("PassingGrade", d.PassingGrade),
            MaxAttempts = section.GetValue("MaxAttempts", d.MaxAttempts),
            TutorWeight = section.GetValue("TutorWeight", d.TutorWeight),
            RequireTutorEvaluation = section.GetValue("RequireTutorEvaluation", d.RequireTutorEvaluation),
            EarliestDaysBeforeEnd = section.GetValue("EarliestDaysBeforeEnd", d.EarliestDaysBeforeEnd),
            LatestDaysAfterEnd = section.GetValue("LatestDaysAfterEnd", d.LatestDaysAfterEnd),
            RetakeLatestDaysAfterEnd = section.GetValue("RetakeLatestDaysAfterEnd", d.RetakeLatestDaysAfterEnd),
            MinValidatedReportRatio = section.GetValue("MinValidatedReportRatio", d.MinValidatedReportRatio),
        };

        if (policy.PassingGrade is < 0 or > 20)
            throw new InvalidOperationException("Defenses:PassingGrade doit être compris entre 0 et 20.");
        if (policy.MaxAttempts < 1)
            throw new InvalidOperationException("Defenses:MaxAttempts doit être au moins 1.");
        if (policy.TutorWeight is < 0 or > 1)
            throw new InvalidOperationException("Defenses:TutorWeight doit être compris entre 0 et 1.");
        if (policy.MinValidatedReportRatio is < 0 or > 1)
            throw new InvalidOperationException("Defenses:MinValidatedReportRatio doit être compris entre 0 et 1.");

        return policy;
    }
}
