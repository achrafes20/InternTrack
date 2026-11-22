using FluentAssertions;
using InternTrack.Application.Features.Defenses;
using InternTrack.Application.Features.Defenses.DTOs;
using InternTrack.Application.Features.Defenses.Services;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Infrastructure.Persistence;
using Xunit;
using static InternTrack.Application.UnitTests.TestHelpers;

namespace InternTrack.Application.UnitTests.Defenses;

public class DefenseConflictDetectionTests
{
    private static readonly DateTime SlotStart = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Horloge du service : la veille du créneau de référence (les soutenances se planifient dans le futur).</summary>
    private readonly TestClock _clock = new(SlotStart.AddDays(-1));

    private async Task<(InternTrackDbContext Context, DefenseService Service, Internship First, Internship Second)> ArrangeAsync()
    {
        var context = CreateInMemoryDbContext();
        var identity = CreateIdentityMock(
            Teacher("prof1", "Professeur Un"),
            Teacher("prof2", "Professeur Deux"),
            Teacher("prof3", "Professeur Trois"),
            Teacher("prof4", "Professeur Quatre", UserRole.ResponsableFiliere),
            Student("std1", "Etudiant Un", Guid.Empty),
            Student("std2", "Etudiant Deux", Guid.Empty));

        var service = new DefenseService(context, identity.Object, LenientDefensePolicy, _clock);

        var track = new AcademicTrack("GI", "Génie Info", "");
        var company = new Company("Tech", "IT", "Casablanca", "Addr", "tech@test.com");
        await context.AcademicTracks.AddAsync(track);
        await context.Companies.AddAsync(company);

        var first = CreateInProgressInternship("std1", company.Id, track.Id, "Stage 1");
        var second = CreateInProgressInternship("std2", company.Id, track.Id, "Stage 2");
        await context.Internships.AddRangeAsync(first, second);
        await context.SaveChangesAsync(CancellationToken.None);

        return (context, service, first, second);
    }

    private static List<JuryMemberInput> Jury(string president, string other) => new()
    {
        new(president, null, DefenseRole.President),
        new(other, null, DefenseRole.Rapporteur)
    };

    [Fact]
    public async Task ScheduleDefense_WhenClassroomIsAlreadyTaken_ShouldReturnConflictFailure()
    {
        var (context, service, first, second) = await ArrangeAsync();
        using var _ = context;

        var res1 = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        res1.IsSuccess.Should().BeTrue(res1.IsFailure ? res1.Error : string.Empty);

        var res2 = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(second.Id, SlotStart.AddMinutes(15), 45, "salle a ", Jury("prof3", "prof4")), "admin", CancellationToken.None);

        res2.IsFailure.Should().BeTrue();
        res2.Error.Should().Contain("Salle A").And.Contain("déjà réservée");
    }

    [Fact]
    public async Task ScheduleDefense_WhenJuryMemberHasConflict_ShouldReturnFailure()
    {
        var (context, service, first, second) = await ArrangeAsync();
        using var _ = context;

        await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        var res2 = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(second.Id, SlotStart.AddMinutes(15), 45, "Salle B", Jury("prof3", "prof1")), "admin", CancellationToken.None);

        res2.IsFailure.Should().BeTrue();
        res2.Error.Should().Contain("Professeur Un").And.Contain("fait déjà partie d'un jury");
    }

    [Fact]
    public async Task ScheduleDefense_WhenSlotsAreConsecutive_ShouldSucceed()
    {
        var (context, service, first, second) = await ArrangeAsync();
        using var _ = context;

        await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        var res2 = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(second.Id, SlotStart.AddMinutes(45), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        res2.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ScheduleDefense_ShouldStoreRolesAndResolvedNames()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var jury = new List<JuryMemberInput>
        {
            new("prof1", "Nom envoyé par le client (ignoré)", DefenseRole.President),
            new("prof2", null, DefenseRole.Examinateur),
            new(null, "Tuteur Externe", DefenseRole.TuteurEntreprise)
        };

        var result = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 60, "Salle A", jury), "admin", CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);
        result.Value!.StudentName.Should().Be("Etudiant Un");
        result.Value.JuryMembers.Should().HaveCount(3);
        result.Value.JuryMembers.Single(m => m.Role == DefenseRole.President).TeacherFullName.Should().Be("Professeur Un");
        result.Value.JuryMembers.Single(m => m.Role == DefenseRole.TuteurEntreprise).TeacherId.Should().StartWith("external:");
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.SoutenancePlanifiee);
    }

    [Theory]
    [InlineData("unknown-teacher", "prof2", "enseignant enregistré")]
    [InlineData("prof1", "prof1", "deux places")]
    public async Task ScheduleDefense_WithInvalidJury_ShouldFail(string president, string other, string expectedError)
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var result = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury(president, other)), "admin", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(expectedError);
    }

    [Fact]
    public async Task ScheduleDefense_WithoutPresident_ShouldFail()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var jury = new List<JuryMemberInput>
        {
            new("prof1", null, DefenseRole.Rapporteur),
            new("prof2", null, DefenseRole.Examinateur)
        };

        var result = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", jury), "admin", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exactement un président");
    }

    [Fact]
    public async Task CheckConflict_ShouldUseRequestedDurationAndIgnoreOwnDefense()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        // Créneau débutant 2 h avant, durée 3 h : il chevauche la soutenance existante.
        var overlapping = await service.CheckConflictAsync(new CheckConflictRequest
        {
            ScheduledAt = SlotStart.AddHours(-2),
            DurationMinutes = 180,
            Classroom = "Salle A",
            JuryTeacherIds = new() { "prof1" }
        }, CancellationToken.None);

        overlapping.HasConflict.Should().BeTrue();
        overlapping.ConflictMessages.Should().HaveCount(2);

        // Replanification de la même soutenance : elle ne doit pas entrer en conflit avec elle-même.
        var selfCheck = await service.CheckConflictAsync(new CheckConflictRequest
        {
            ScheduledAt = SlotStart,
            DurationMinutes = 45,
            Classroom = "Salle A",
            JuryTeacherIds = new() { "prof1" },
            InternshipId = first.Id
        }, CancellationToken.None);

        selfCheck.HasConflict.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluation_BeforeDefenseStarts_ShouldFail()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;
        AddThesis(context, first.Id);

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        var result = await service.SubmitEvaluationAsync(scheduled.Value!.Id, new SubmitEvaluationCommand(15, "Bien"), "prof1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("après le début");
    }

    [Fact]
    public async Task RecordGrade_WithMissingJuryEvaluations_ShouldFail()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;
        AddThesis(context, first.Id);

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        _clock.Now = SlotStart.AddHours(2);
        await service.SubmitEvaluationAsync(scheduled.Value!.Id, new SubmitEvaluationCommand(15, null), "prof1", CancellationToken.None);

        var result = await service.RecordGradeAsync(scheduled.Value.Id, new RecordGradeCommand("Bien"), "admin", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Évaluation manquante").And.Contain("Professeur Deux");
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.SoutenancePlanifiee);
    }

    [Fact]
    public async Task RecordGrade_WithoutThesis_ShouldFail()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        _clock.Now = SlotStart.AddHours(2);

        var evaluation = await service.SubmitEvaluationAsync(scheduled.Value!.Id, new SubmitEvaluationCommand(15, null), "prof1", CancellationToken.None);
        evaluation.IsFailure.Should().BeTrue();
        evaluation.Error.Should().Contain("mémoire");

        var result = await service.RecordGradeAsync(scheduled.Value.Id, new RecordGradeCommand(null), "admin", CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("mémoire");
    }

    [Fact]
    public async Task RecordGrade_IsComputedFromJuryEvaluations_AndCompletesInternship()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;
        AddThesis(context, first.Id);

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        _clock.Now = SlotStart.AddHours(2);
        await service.SubmitEvaluationAsync(scheduled.Value!.Id, new SubmitEvaluationCommand(16, null), "prof1", CancellationToken.None);
        await service.SubmitEvaluationAsync(scheduled.Value.Id, new SubmitEvaluationCommand(17, null), "prof2", CancellationToken.None);

        var result = await service.RecordGradeAsync(scheduled.Value.Id, new RecordGradeCommand("Très bien"), "admin", CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.Termine);
        context.Defenses.Single(d => d.Id == scheduled.Value.Id).FinalGrade.Should().Be(16.5m);
    }

    [Fact]
    public async Task FailingGrade_Postpones_ThenRetake_ThenSecondFailure_IsFinal()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;
        AddThesis(context, first.Id);

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        var defenseId = scheduled.Value!.Id;
        _clock.Now = SlotStart.AddHours(2);
        await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(8, null), "prof1", CancellationToken.None);
        await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(9, null), "prof2", CancellationToken.None);

        (await service.RecordGradeAsync(defenseId, new RecordGradeCommand(null), "admin", CancellationToken.None)).IsSuccess.Should().BeTrue();
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.Ajourne);

        // Rattrapage : nouvelle session, la note précédente est conservée et le jury évalue à nouveau.
        var retakeAt = SlotStart.AddDays(30);
        var retake = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, retakeAt, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        retake.IsSuccess.Should().BeTrue(retake.IsFailure ? retake.Error : string.Empty);
        retake.Value!.Attempt.Should().Be(2);
        retake.Value.PreviousAttemptGrade.Should().Be(8.5m);
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.SoutenancePlanifiee);
        context.DefenseEvaluations.Count(e => e.DefenseId == defenseId).Should().Be(0);

        _clock.Now = retakeAt.AddHours(2);
        await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(7, null), "prof1", CancellationToken.None);
        await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(8, null), "prof2", CancellationToken.None);
        (await service.RecordGradeAsync(defenseId, new RecordGradeCommand(null), "admin", CancellationToken.None)).IsSuccess.Should().BeTrue();

        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.NonValide);
        var noThirdSession = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, retakeAt.AddDays(10), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        noThirdSession.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GradeCorrection_CanTurnAFailureIntoAPass()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;
        AddThesis(context, first.Id);

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        _clock.Now = SlotStart.AddHours(2);
        await service.SubmitEvaluationAsync(scheduled.Value!.Id, new SubmitEvaluationCommand(9, null), "prof1", CancellationToken.None);
        await service.SubmitEvaluationAsync(scheduled.Value.Id, new SubmitEvaluationCommand(9.5m, null), "prof2", CancellationToken.None);
        await service.RecordGradeAsync(scheduled.Value.Id, new RecordGradeCommand(null), "admin", CancellationToken.None);
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.Ajourne);

        var corrected = await service.CorrectGradeAsync(scheduled.Value.Id, new CorrectGradeCommand(10.5m, null, "Erreur de report lors de la délibération"), "admin", CancellationToken.None);

        corrected.IsSuccess.Should().BeTrue();
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.Termine);
    }

    [Fact]
    public async Task CancelDefense_FreesTheSlot_AndRevertsToInProgress()
    {
        var (context, service, first, second) = await ArrangeAsync();
        using var __ = context;

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        (await service.CancelDefenseAsync(scheduled.Value!.Id, "", "admin", CancellationToken.None)).IsFailure.Should().BeTrue("motif obligatoire");
        (await service.CancelDefenseAsync(scheduled.Value.Id, "Absence justifiée de l'élève", "admin", CancellationToken.None)).IsSuccess.Should().BeTrue();

        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.EnCours);

        // Le créneau, la salle et le jury sont libérés.
        var other = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(second.Id, SlotStart, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        other.IsSuccess.Should().BeTrue(other.IsFailure ? other.Error : string.Empty);

        // La soutenance reportée se replanifie sur un autre créneau.
        var again = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart.AddDays(7), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        again.IsSuccess.Should().BeTrue(again.IsFailure ? again.Error : string.Empty);
        again.Value!.CancelledAt.Should().BeNull();
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.SoutenancePlanifiee);
    }

    [Fact]
    public async Task Schedule_InThePast_ShouldFail()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var result = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, SlotStart.AddDays(-2), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("date future");
    }

    [Fact]
    public async Task Schedule_OutsideTheRegulatoryWindow_ShouldFail()
    {
        var context = CreateInMemoryDbContext();
        using var _ = context;
        var identity = CreateIdentityMock(Teacher("prof1", "Professeur Un"), Teacher("prof2", "Professeur Deux"), Student("std1", "Etudiant Un", Guid.Empty));
        var service = new DefenseService(context, identity.Object, new DefensePolicy { MinValidatedReportRatio = 0m }, new TestClock(DateTime.UtcNow));

        var internship = CreateInProgressInternship("std1", Guid.NewGuid(), Guid.NewGuid());
        context.Internships.Add(internship);
        await context.SaveChangesAsync();

        // Fin du stage dans 3 mois : une soutenance la semaine prochaine est trop précoce (au plus tôt 30 jours avant la fin).
        var tooEarly = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(internship.Id, DateTime.UtcNow.AddDays(7), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        tooEarly.IsFailure.Should().BeTrue();
        tooEarly.Error.Should().Contain("avant la fin du stage");

        var tooLate = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(internship.Id, internship.Period.EndDate.AddDays(120), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        tooLate.IsFailure.Should().BeTrue();
        tooLate.Error.Should().Contain("au plus tard");

        var inWindow = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(internship.Id, internship.Period.EndDate.AddDays(-7), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);
        inWindow.IsSuccess.Should().BeTrue(inWindow.IsFailure ? inWindow.Error : string.Empty);
    }

    [Fact]
    public async Task Schedule_WithInsufficientValidatedReports_ShouldFail()
    {
        var context = CreateInMemoryDbContext();
        using var _ = context;
        var identity = CreateIdentityMock(Teacher("prof1", "Professeur Un"), Teacher("prof2", "Professeur Deux"), Student("std1", "Etudiant Un", Guid.Empty));

        var internship = CreateInProgressInternship("std1", Guid.NewGuid(), Guid.NewGuid());
        context.Internships.Add(internship);
        await context.SaveChangesAsync();

        // Huit semaines écoulées : 4 rapports validés exigés (ratio 0,5), aucun déposé.
        var clock = new TestClock(internship.Period.StartDate.AddDays(7 * 8 - 1));
        var strict = new DefenseService(context, identity.Object, new DefensePolicy
        {
            EarliestDaysBeforeEnd = 100_000, LatestDaysAfterEnd = 100_000, MinValidatedReportRatio = 0.5m
        }, clock);

        var result = await strict.ScheduleDefenseAsync(new ScheduleDefenseCommand(internship.Id, clock.GetUtcNow().UtcDateTime.AddDays(3), 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Suivi insuffisant").And.Contain("0 rapport(s)").And.Contain("4 exigé(s)");
    }
}
