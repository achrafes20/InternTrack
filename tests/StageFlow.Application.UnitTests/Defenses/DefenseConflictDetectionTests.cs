using FluentAssertions;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Application.Features.Defenses.Services;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Persistence;
using Xunit;
using static StageFlow.Application.UnitTests.TestHelpers;

namespace StageFlow.Application.UnitTests.Defenses;

public class DefenseConflictDetectionTests
{
    private static readonly DateTime SlotStart = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    private static async Task<(StageFlowDbContext Context, DefenseService Service, Internship First, Internship Second)> ArrangeAsync()
    {
        var context = CreateInMemoryDbContext();
        var identity = CreateIdentityMock(
            Teacher("prof1", "Professeur Un"),
            Teacher("prof2", "Professeur Deux"),
            Teacher("prof3", "Professeur Trois"),
            Teacher("prof4", "Professeur Quatre", UserRole.ResponsableFiliere),
            Student("std1", "Etudiant Un", Guid.Empty),
            Student("std2", "Etudiant Deux", Guid.Empty));

        var service = new DefenseService(context, identity.Object);

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
    public async Task RecordGrade_BeforeDefenseStarts_ShouldFail()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var future = DateTime.UtcNow.AddDays(3);
        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, future, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        var result = await service.RecordGradeAsync(scheduled.Value!.Id, new RecordGradeCommand(15, "Bien"), "admin", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("après le début");
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.SoutenancePlanifiee);
    }

    [Fact]
    public async Task RecordGrade_AfterDefense_ShouldCompleteInternship()
    {
        var (context, service, first, _) = await ArrangeAsync();
        using var __ = context;

        var past = DateTime.UtcNow.AddHours(-2);
        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(first.Id, past, 45, "Salle A", Jury("prof1", "prof2")), "admin", CancellationToken.None);

        var result = await service.RecordGradeAsync(scheduled.Value!.Id, new RecordGradeCommand(16.5m, "Très bien"), "admin", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Internships.Single(i => i.Id == first.Id).Status.Should().Be(InternshipStatus.Termine);
        context.Defenses.Single(d => d.Id == scheduled.Value.Id).FinalGrade.Should().Be(16.5m);
    }
}
