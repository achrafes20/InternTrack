using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Application.Features.Defenses.Services;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Persistence;
using Xunit;

namespace StageFlow.Application.UnitTests.Defenses;

public class DefenseConflictDetectionTests
{
    private StageFlowDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StageFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StageFlowDbContext(options);
    }

    [Fact]
    public async Task ScheduleDefense_WhenClassroomIsAlreadyTaken_ShouldReturnConflictFailure()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DefenseService(context);

        var track = new AcademicTrack("GI", "Génie Info", "");
        var company = new Company("Tech", "IT", "Casablanca", "Addr", "tech@test.com");
        await context.AcademicTracks.AddAsync(track);
        await context.Companies.AddAsync(company);

        var internship1 = new Internship("Stage 1", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "std1", company.Id, track.Id, "Tut1", "tut1@test.com");
        var internship2 = new Internship("Stage 2", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "std2", company.Id, track.Id, "Tut2", "tut2@test.com");

        internship1.SubmitForApproval("std1");
        internship1.Approve("admin");
        internship1.StartInternship("admin");

        internship2.SubmitForApproval("std2");
        internship2.Approve("admin");
        internship2.StartInternship("admin");

        await context.Internships.AddRangeAsync(internship1, internship2);
        await context.SaveChangesAsync(CancellationToken.None);

        var scheduleTime = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        // Première soutenance programmée en Salle A
        var cmd1 = new ScheduleDefenseCommand(
            internship1.Id,
            scheduleTime,
            45,
            "Salle A",
            new List<JuryMemberInput>
            {
                new("prof1", "Professeur Un", DefenseRole.President),
                new("prof2", "Professeur Deux", DefenseRole.Rapporteur)
            });

        var res1 = await service.ScheduleDefenseAsync(cmd1, "admin", CancellationToken.None);
        res1.IsSuccess.Should().BeTrue();

        // Deuxième soutenance sur le MÊME créneau et la MÊME salle
        var cmd2 = new ScheduleDefenseCommand(
            internship2.Id,
            scheduleTime.AddMinutes(15), // Chevauchement partiel
            45,
            "Salle A",
            new List<JuryMemberInput>
            {
                new("prof3", "Professeur Trois", DefenseRole.President),
                new("prof4", "Professeur Quatre", DefenseRole.Rapporteur)
            });

        var res2 = await service.ScheduleDefenseAsync(cmd2, "admin", CancellationToken.None);

        res2.IsFailure.Should().BeTrue();
        res2.Error.Should().Contain("Salle A");
        res2.Error.Should().Contain("déjà réservée");
    }

    [Fact]
    public async Task ScheduleDefense_WhenJuryMemberHasConflict_ShouldReturnFailure()
    {
        using var context = CreateInMemoryDbContext();
        var service = new DefenseService(context);

        var track = new AcademicTrack("GI", "Génie Info", "");
        var company = new Company("Tech", "IT", "Casablanca", "Addr", "tech@test.com");
        await context.AcademicTracks.AddAsync(track);
        await context.Companies.AddAsync(company);

        var internship1 = new Internship("Stage 1", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "std1", company.Id, track.Id, "Tut1", "tut1@test.com");
        var internship2 = new Internship("Stage 2", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "std2", company.Id, track.Id, "Tut2", "tut2@test.com");

        internship1.SubmitForApproval("std1");
        internship1.Approve("admin");
        internship1.StartInternship("admin");

        internship2.SubmitForApproval("std2");
        internship2.Approve("admin");
        internship2.StartInternship("admin");

        await context.Internships.AddRangeAsync(internship1, internship2);
        await context.SaveChangesAsync(CancellationToken.None);

        var scheduleTime = new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc);

        // Soutenance 1 en Salle A avec prof1
        var cmd1 = new ScheduleDefenseCommand(
            internship1.Id,
            scheduleTime,
            45,
            "Salle A",
            new List<JuryMemberInput>
            {
                new("prof1", "Professeur Un", DefenseRole.President),
                new("prof2", "Professeur Deux", DefenseRole.Rapporteur)
            });

        await service.ScheduleDefenseAsync(cmd1, "admin", CancellationToken.None);

        // Soutenance 2 en Salle B MAIS avec le même prof1
        var cmd2 = new ScheduleDefenseCommand(
            internship2.Id,
            scheduleTime.AddMinutes(15),
            45,
            "Salle B",
            new List<JuryMemberInput>
            {
                new("prof1", "Professeur Un", DefenseRole.Examinateur),
                new("prof3", "Professeur Trois", DefenseRole.President)
            });

        var res2 = await service.ScheduleDefenseAsync(cmd2, "admin", CancellationToken.None);

        res2.IsFailure.Should().BeTrue();
        res2.Error.Should().Contain("Professeur Un");
        res2.Error.Should().Contain("fait déjà partie d'un jury");
    }
}

