using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Auth.Models;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Application.Features.Defenses.Services;
using StageFlow.Application.Features.InternshipOffers.Services;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using Xunit;

namespace StageFlow.Api.IntegrationTests.PostgreSql;

/// <summary>
/// Garanties qui n'existent qu'au niveau de PostgreSQL et que la base en mémoire ne peut pas vérifier :
/// migrations, trigger append-only, contraintes d'unicité et d'exclusion, concurrence optimiste (xmin),
/// verrou applicatif et traduction SQL des requêtes.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlDatabaseTests
{
    private static readonly DateTime Slot = DateTime.UtcNow.Date.AddDays(10).AddHours(10);

    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlDatabaseTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_ApplyOnPostgreSql16_WithoutPendingModelChanges()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        await using var context = _fixture.CreateContext(database);

        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public async Task AuditLog_IsAppendOnly_UpdateAndDeleteAreRejectedByTrigger()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        await using var context = _fixture.CreateContext(database);

        context.AuditLogs.Add(new AuditLog("Internship", Guid.NewGuid().ToString(), "Added", "user", "user@ensa.ma"));
        await context.SaveChangesAsync();

        var update = () => context.Database.ExecuteSqlRawAsync(@"UPDATE ""AuditLogs"" SET ""Action"" = 'Falsifié'");
        var delete = () => context.Database.ExecuteSqlRawAsync(@"DELETE FROM ""AuditLogs""");

        await update.Should().ThrowAsync<PostgresException>();
        await delete.Should().ThrowAsync<PostgresException>();
        (await context.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task OverlappingDefensesInSameRoom_AreRejectedByExclusionConstraint_CaseInsensitive()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        await using var context = _fixture.CreateContext(database);
        var (_, _, internships) = await PostgreSqlFixture.SeedInProgressInternshipsAsync(context, 3);

        context.Defenses.Add(new Defense(internships[0].Id, Slot, "Salle A", 45));
        await context.SaveChangesAsync();

        // Chevauchement partiel, même salle écrite différemment : refusé par la base.
        context.Defenses.Add(new Defense(internships[1].Id, Slot.AddMinutes(30), "  salle a ", 45));
        var overlapping = () => context.SaveChangesAsync();

        var error = await overlapping.Should().ThrowAsync<DbUpdateException>();
        var pg = error.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be(PostgresErrorCodes.ExclusionViolation);
        pg.ConstraintName.Should().Be("EX_Defenses_Classroom_Slot");

        // Créneau consécutif ([10:00, 10:45[ puis [10:45, 11:30[) : autorisé.
        context.ChangeTracker.Clear();
        context.Defenses.Add(new Defense(internships[2].Id, Slot.AddMinutes(45), "Salle A", 45));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ConcurrentUpdatesOfSameInternship_SecondWriterGetsConcurrencyException()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        Guid internshipId;

        await using (var setup = _fixture.CreateContext(database))
        {
            var track = new AcademicTrack("GI", "Génie Informatique", "");
            var company = new Company("Tech", "IT", "Rabat", "Adresse", "c@tech.ma");
            var internship = new Internship("Stage", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(4), "std", company.Id, track.Id, "Tuteur", "t@tech.ma");
            internship.SubmitForApproval("std");
            setup.AddRange(track, company, internship);
            await setup.SaveChangesAsync();
            internshipId = internship.Id;
        }

        // Deux responsables ouvrent le même dossier « Proposé »...
        await using var first = _fixture.CreateContext(database);
        await using var second = _fixture.CreateContext(database);
        var seenByFirst = await first.Internships.SingleAsync(i => i.Id == internshipId);
        var seenBySecond = await second.Internships.SingleAsync(i => i.Id == internshipId);

        // ... le premier valide, le second refuse sur la base d'un état devenu obsolète.
        seenByFirst.Approve("head-1").IsSuccess.Should().BeTrue();
        await first.SaveChangesAsync();

        seenBySecond.Reject("head-2", "Motif").IsSuccess.Should().BeTrue();
        var staleWrite = () => second.SaveChangesAsync();

        await staleWrite.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var check = _fixture.CreateContext(database);
        (await check.Internships.SingleAsync(i => i.Id == internshipId)).Status.Should().Be(InternshipStatus.Valide);
    }

    [Fact]
    public async Task DuplicateWeeklyReport_IsRejectedByUniqueIndex()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        await using var context = _fixture.CreateContext(database);
        var (_, _, internships) = await PostgreSqlFixture.SeedInProgressInternshipsAsync(context, 1);

        context.ProgressReports.Add(new ProgressReport(internships[0].Id, 3, "S3", "Activités", "", "", ""));
        await context.SaveChangesAsync();

        context.ProgressReports.Add(new ProgressReport(internships[0].Id, 3, "S3 bis", "Activités", "", "", ""));
        var duplicate = () => context.SaveChangesAsync();

        var error = await duplicate.Should().ThrowAsync<DbUpdateException>();
        error.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task OfferSearch_IsCaseInsensitive_WhenTranslatedToSql()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        await using var context = _fixture.CreateContext(database);
        var track = new AcademicTrack("GI", "Génie Informatique", "");
        var company = new Company("Capgemini Morocco", "IT", "Casablanca", "Adresse", "c@capgemini.ma");
        context.AddRange(track, company);
        context.InternshipOffers.Add(new InternshipOffer("Développeur React", "Front-end TypeScript", "React, TypeScript",
            "Casablanca", 24, 5000m, DateTime.UtcNow.AddMonths(2), company.Id, track.Id));
        await context.SaveChangesAsync();

        var service = new InternshipOfferService(context);
        var result = await service.GetOffersAsync("rEaCt", "CAPGEMINI", "casablanca", "typescript", null, false, 1, 10, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Développeur React");
    }

    [Fact]
    public async Task ConcurrentScheduling_SameRoom_OnlyOneDefenseIsCreated()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        List<Internship> internships;
        await using (var setup = _fixture.CreateContext(database))
            (_, _, internships) = await PostgreSqlFixture.SeedInProgressInternshipsAsync(setup, 2);

        var results = await ScheduleConcurrentlyAsync(database,
            new ScheduleDefenseCommand(internships[0].Id, Slot, 60, "Amphi 1", Jury("prof1", "prof2")),
            new ScheduleDefenseCommand(internships[1].Id, Slot.AddMinutes(15), 60, "Amphi 1", Jury("prof3", "prof4")));

        results.Count(r => r.IsSuccess).Should().Be(1);
        await using var check = _fixture.CreateContext(database);
        (await check.Defenses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentScheduling_SameTeacherInTwoRooms_OnlyOneDefenseIsCreated()
    {
        var database = await _fixture.CreateMigratedDatabaseAsync();
        List<Internship> internships;
        await using (var setup = _fixture.CreateContext(database))
            (_, _, internships) = await PostgreSqlFixture.SeedInProgressInternshipsAsync(setup, 2);

        // Salles différentes : seule la sérialisation applicative (verrou PostgreSQL) empêche le double booking du jury.
        var results = await ScheduleConcurrentlyAsync(database,
            new ScheduleDefenseCommand(internships[0].Id, Slot, 60, "Salle B1", Jury("prof1", "prof2")),
            new ScheduleDefenseCommand(internships[1].Id, Slot.AddMinutes(15), 60, "Salle B2", Jury("prof1", "prof3")));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Contain("fait déjà partie d'un jury");
    }

    private async Task<List<Domain.Common.Result<DefenseDto>>> ScheduleConcurrentlyAsync(string database, params ScheduleDefenseCommand[] commands)
    {
        var identity = TeachersIdentity();
        var tasks = commands.Select(async command =>
        {
            await using var context = _fixture.CreateContext(database);
            var service = new DefenseService(context, identity);
            try
            {
                return await service.ScheduleDefenseAsync(command, "head", CancellationToken.None);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException)
            {
                // Filet de sécurité de la base (contrainte d'exclusion) : l'API le traduit en HTTP 409.
                return Domain.Common.Result<DefenseDto>.Failure("Conflit détecté par la base de données.");
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private static List<JuryMemberInput> Jury(string president, string other) => new()
    {
        new(president, null, DefenseRole.President),
        new(other, null, DefenseRole.Examinateur)
    };

    private static IIdentityService TeachersIdentity()
    {
        var teachers = Enumerable.Range(1, 4)
            .Select(i => new UserDto($"prof{i}", $"prof{i}@ensa.ma", "Prof", $"Numéro {i}", $"Prof Numéro {i}", UserRole.Encadrant, null, null))
            .ToList();

        var mock = new Mock<IIdentityService>();
        mock.Setup(s => s.GetUsersInRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(teachers);
        mock.Setup(s => s.GetUserNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        return mock.Object;
    }
}
