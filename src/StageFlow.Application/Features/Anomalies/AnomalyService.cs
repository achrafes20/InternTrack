using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;

namespace StageFlow.Application.Features.Anomalies;

public enum AnomalySeverity
{
    /// <summary>Viole une règle métier : à corriger.</summary>
    Error = 1,

    /// <summary>Situation probablement oubliée : à vérifier.</summary>
    Warning = 2
}

public record AnomalyDto(
    string Code,
    AnomalySeverity Severity,
    string Title,
    string Detail,
    string Resolution,
    Guid? InternshipId = null,
    string? InternshipTitle = null,
    string? StudentName = null,
    string? UserId = null);

public interface IAnomalyService
{
    /// <summary>
    /// Détecte les incohérences des données existantes (notamment antérieures aux règles actuelles).
    /// Avec <paramref name="trackId"/>, le rapport est limité à une filière (responsable de filière).
    /// </summary>
    Task<List<AnomalyDto>> DetectAsync(Guid? trackId, CancellationToken cancellationToken);
}

public class AnomalyService : IAnomalyService
{
    private const int StaleStartToleranceDays = 14;
    private const int OverrunToleranceDays = 7;

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly ITimeZoneService _timeZone;

    public AnomalyService(IApplicationDbContext context, IIdentityService identityService, ITimeZoneService timeZone)
    {
        _context = context;
        _identityService = identityService;
        _timeZone = timeZone;
    }

    public async Task<List<AnomalyDto>> DetectAsync(Guid? trackId, CancellationToken cancellationToken)
    {
        var internships = await _context.Internships.AsNoTracking()
            .Include(i => i.Convention)
            .Include(i => i.Defense)
            .Where(i => !trackId.HasValue || i.AcademicTrackId == trackId.Value)
            .ToListAsync(cancellationToken);

        var names = await _identityService.GetUserNamesAsync(internships.Select(i => i.StudentId).Distinct(), cancellationToken);
        string Student(Internship i) => names.GetValueOrDefault(i.StudentId) ?? "Élève inconnu";

        var today = _timeZone.LocalToday;
        var anomalies = new List<AnomalyDto>();

        // 1. Plusieurs dossiers engagés pour un même élève (règle « un seul stage à la fois »).
        foreach (var group in internships
                     .Where(i => Internship.EngagedStatuses.Contains(i.Status))
                     .GroupBy(i => i.StudentId)
                     .Where(g => g.Count() > 1))
        {
            var list = string.Join(", ", group.OrderBy(i => i.Status).Select(i => $"« {i.Title} » ({i.Status})"));
            foreach (var internship in group.Where(i => i.Status is InternshipStatus.Propose or InternshipStatus.Valide))
            {
                anomalies.Add(new AnomalyDto(
                    "MULTIPLE_ENGAGED", AnomalySeverity.Error,
                    "Plusieurs dossiers engagés pour le même élève",
                    $"{Student(internship)} a {group.Count()} dossiers engagés : {list}.",
                    "Conserver le dossier pertinent et annuler les autres (bouton « Annuler le dossier »).",
                    internship.Id, internship.Title, Student(internship), internship.StudentId));
            }

            // Plusieurs stages déjà démarrés : aucune annulation automatique possible, décision pédagogique.
            var running = group.Where(i => Internship.RunningStatuses.Contains(i.Status)).ToList();
            if (running.Count > 1)
            {
                anomalies.Add(new AnomalyDto(
                    "MULTIPLE_RUNNING", AnomalySeverity.Error,
                    "Plusieurs stages en cours pour le même élève",
                    $"{Student(running[0])} suit simultanément : {string.Join(", ", running.Select(i => $"« {i.Title} »"))}.",
                    "Arbitrer avec l'élève et l'encadrant ; annuler le stage abandonné s'il n'a pas de soutenance planifiée.",
                    running[0].Id, running[0].Title, Student(running[0]), running[0].StudentId));
            }
        }

        foreach (var i in internships)
        {
            var isRunning = Internship.RunningStatuses.Contains(i.Status);

            // 2. Stage démarré sans convention signée par les trois parties.
            if (isRunning && i.Convention?.SignedAt == null)
            {
                anomalies.Add(new AnomalyDto(
                    "RUNNING_WITHOUT_SIGNED_CONVENTION", AnomalySeverity.Error,
                    "Stage démarré sans convention signée",
                    $"« {i.Title} » est en cours alors que la convention n'est pas signée par les trois parties.",
                    "Recueillir les signatures manquantes (élève, école, lien entreprise ou scan signé).",
                    i.Id, i.Title, Student(i), i.StudentId));
            }

            // 3. Stage en cours sans encadrant pédagogique.
            if (isRunning && string.IsNullOrEmpty(i.AcademicSupervisorId))
            {
                anomalies.Add(new AnomalyDto(
                    "RUNNING_WITHOUT_SUPERVISOR", AnomalySeverity.Error,
                    "Stage en cours sans encadrant",
                    $"Aucun encadrant pédagogique n'est affecté au stage « {i.Title} » : ses rapports ne sont relus par personne.",
                    "Affecter un encadrant de la filière.",
                    i.Id, i.Title, Student(i), i.StudentId));
            }

            // 4. Période terminée mais stage toujours « En cours » (soutenance non planifiée).
            if (i.Status == InternshipStatus.EnCours && _timeZone.ToLocal(i.Period.EndDate).Date < today.AddDays(-OverrunToleranceDays))
            {
                anomalies.Add(new AnomalyDto(
                    "ENDED_WITHOUT_DEFENSE", AnomalySeverity.Warning,
                    "Stage terminé sans soutenance planifiée",
                    $"« {i.Title} » s'est terminé le {_timeZone.ToLocal(i.Period.EndDate):dd/MM/yyyy} et aucune soutenance n'est planifiée.",
                    "Planifier la soutenance.",
                    i.Id, i.Title, Student(i), i.StudentId));
            }

            // 5. Stage validé dont la date de début est largement dépassée sans démarrage.
            if (i.Status == InternshipStatus.Valide && _timeZone.ToLocal(i.Period.StartDate).Date < today.AddDays(-StaleStartToleranceDays))
            {
                anomalies.Add(new AnomalyDto(
                    "VALIDATED_NOT_STARTED", AnomalySeverity.Warning,
                    "Stage validé jamais démarré",
                    $"« {i.Title} » devait commencer le {_timeZone.ToLocal(i.Period.StartDate):dd/MM/yyyy} mais n'a pas été démarré.",
                    "Démarrer le stage une fois la convention signée, ou l'annuler s'il n'a pas eu lieu.",
                    i.Id, i.Title, Student(i), i.StudentId));
            }

            // 6. Incohérences entre le statut et la soutenance.
            if (i.Status == InternshipStatus.SoutenancePlanifiee && i.Defense == null)
            {
                anomalies.Add(new AnomalyDto(
                    "DEFENSE_MISSING", AnomalySeverity.Error,
                    "Soutenance annoncée mais absente",
                    $"« {i.Title} » est au statut « Soutenance planifiée » sans soutenance enregistrée.",
                    "Planifier la soutenance depuis la page Soutenances.",
                    i.Id, i.Title, Student(i), i.StudentId));
            }

            if (i.Defense?.FinalGrade != null && i.Status != InternshipStatus.Termine)
            {
                anomalies.Add(new AnomalyDto(
                    "GRADED_NOT_CLOSED", AnomalySeverity.Error,
                    "Soutenance notée mais stage non clôturé",
                    $"« {i.Title} » a une note finale ({i.Defense.FinalGrade}/20) mais son statut est {i.Status}.",
                    "Vérifier le dossier : la note aurait dû clôturer le stage.",
                    i.Id, i.Title, Student(i), i.StudentId));
            }

            if (i.Status == InternshipStatus.Valide && i.Convention == null)
            {
                anomalies.Add(new AnomalyDto(
                    "VALIDATED_WITHOUT_CONVENTION", AnomalySeverity.Error,
                    "Dossier validé sans convention",
                    $"« {i.Title} » est validé mais aucune convention n'a été générée.",
                    "Contacter l'administrateur technique : la convention est normalement créée à la validation.",
                    i.Id, i.Title, Student(i), i.StudentId));
            }
        }

        // 7. Comptes incomplets : élèves et responsables sans filière ne voient aucun dossier.
        var users = await _identityService.GetUsersInRolesAsync(new[] { UserRole.Etudiant, UserRole.ResponsableFiliere }, cancellationToken);
        foreach (var user in users.Where(u => u.IsActive && !u.AcademicTrackId.HasValue))
        {
            if (trackId.HasValue) break; // sans filière, ils n'appartiennent au périmètre d'aucun responsable
            anomalies.Add(new AnomalyDto(
                "USER_WITHOUT_TRACK", AnomalySeverity.Warning,
                "Compte sans filière",
                $"{user.FullName} ({user.Email}, {user.Role}) n'est rattaché à aucune filière et ne voit aucun dossier.",
                "Attribuer une filière depuis la page Utilisateurs.",
                UserId: user.Id, StudentName: user.FullName));
        }

        return anomalies
            .OrderBy(a => a.Severity)
            .ThenBy(a => a.Code)
            .ThenBy(a => a.StudentName)
            .ToList();
    }
}
