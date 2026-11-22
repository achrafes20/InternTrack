using FluentAssertions;
using InternTrack.Application.Features.Defenses.DTOs;
using InternTrack.Application.Features.Defenses.Services;
using InternTrack.Application.Features.Documents;
using InternTrack.Application.Features.ProgressReports.DTOs;
using InternTrack.Application.Features.ProgressReports.Services;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Infrastructure.Persistence;
using Xunit;
using static InternTrack.Application.UnitTests.TestHelpers;

namespace InternTrack.Application.UnitTests.ProgressReports;

public class ReportAndDocumentRulesTests
{
    private readonly InternTrackDbContext _context = CreateInMemoryDbContext();
    private readonly AcademicTrack _track = new("GI", "Génie Info", "");
    private readonly Company _company = new("Tech", "IT", "Casablanca", "Addr", "tech@test.com");

    public ReportAndDocumentRulesTests()
    {
        _context.AddRange(_track, _company);
        _context.SaveChanges();
    }

    /// <summary>Stage démarré il y a <paramref name="startedDaysAgo"/> jours, pour 20 semaines.</summary>
    private Internship AddRunningInternship(int startedDaysAgo = 15)
    {
        var start = DateTime.UtcNow.Date.AddDays(-startedDaysAgo);
        var internship = new Internship("Stage", "Desc", start, start.AddDays(20 * 7), "std1", _company.Id, _track.Id, "Tuteur", "t@test.com");
        internship.SubmitForApproval("std1");
        internship.Approve("head");
        internship.StartInternship("head");
        _context.Internships.Add(internship);
        _context.SaveChanges();
        return internship;
    }

    private ProgressReportService Reports() => new(_context, CreateIdentityMock().Object, MoroccoTime);

    private static CreateReportCommand Report(int week) => new() { WeekNumber = week, ActivitiesPerformed = "Travail réalisé" };

    [Fact]
    public async Task Reports_CannotBeSubmittedBeforeTheInternshipStarts()
    {
        var internship = new Internship("Stage", "Desc", DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(150), "std1", _company.Id, _track.Id, "Tuteur", "t@test.com");
        internship.SubmitForApproval("std1");
        internship.Approve("head");
        _context.Internships.Add(internship);
        _context.SaveChanges();

        var result = await Reports().CreateReportAsync(internship.Id, Report(1), "std1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("une fois celui-ci démarré");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]   // la semaine 4 n'a pas encore commencé (stage démarré il y a 15 jours)
    [InlineData(21, false)]  // au-delà de la durée du stage
    [InlineData(0, false)]
    public async Task ReportWeek_MustBelongToThePeriod_AndHaveStarted(int week, bool expected)
    {
        var internship = AddRunningInternship(startedDaysAgo: 15);

        var result = await Reports().CreateReportAsync(internship.Id, Report(week), "std1", CancellationToken.None);

        result.IsSuccess.Should().Be(expected, result.IsFailure ? result.Error : string.Empty);
    }

    [Fact]
    public async Task Report_CanBeEditedByItsAuthor_UntilValidated()
    {
        var internship = AddRunningInternship();
        var service = Reports();
        var created = await service.CreateReportAsync(internship.Id, Report(1), "std1", CancellationToken.None);
        var update = new UpdateReportCommand { Title = "S1 corrigée", ActivitiesPerformed = "Version corrigée" };

        (await service.UpdateReportAsync(created.Value!.Id, update, "intrus", CancellationToken.None)).IsFailure.Should().BeTrue();

        var edited = await service.UpdateReportAsync(created.Value.Id, update, "std1", CancellationToken.None);
        edited.IsSuccess.Should().BeTrue();
        edited.Value!.ActivitiesPerformed.Should().Be("Version corrigée");

        (await service.ValidateReportAsync(created.Value.Id, "prof", CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await service.UpdateReportAsync(created.Value.Id, update, "std1", CancellationToken.None)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Thesis_MustBeAPdf_ReplacesThePreviousUpload_AndIsLockedOnceGraded()
    {
        var internship = AddRunningInternship();
        var storage = new InMemoryFileStorage();
        var documents = new DocumentService(_context, storage);

        var notPdf = await documents.UploadAsync(internship.Id, InternshipDocumentKind.FinalThesis, "memoire.pdf", 4,
            new MemoryStream("MZ\x90\0"u8.ToArray()), "std1", CancellationToken.None);
        notPdf.IsFailure.Should().BeTrue("une extension .pdf ne suffit pas : le contenu est vérifié");

        var first = await documents.UploadAsync(internship.Id, InternshipDocumentKind.FinalThesis, "v1.pdf", 1024, new MemoryStream(FakePdf()), "std1", CancellationToken.None);
        var second = await documents.UploadAsync(internship.Id, InternshipDocumentKind.FinalThesis, "v2.pdf", 2048, new MemoryStream(FakePdf(2048)), "std1", CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        var listed = await documents.ListAsync(internship.Id, CancellationToken.None);
        listed.Should().ContainSingle().Which.FileName.Should().Be("v2.pdf");
        storage.Files.Should().HaveCount(1, "le fichier remplacé est supprimé du stockage");

        var opened = await documents.OpenAsync(second.Value!.Id, CancellationToken.None);
        opened.IsSuccess.Should().BeTrue();
        opened.Value!.ContentType.Should().Be("application/pdf");

        var defense = new Defense(internship.Id, DateTime.UtcNow.AddHours(-2), "Salle A");
        defense.RecordGrade(15, null, DateTime.UtcNow, "head");
        _context.Defenses.Add(defense);
        _context.SaveChanges();

        var afterGrade = await documents.UploadAsync(internship.Id, InternshipDocumentKind.FinalThesis, "v3.pdf", 1024, new MemoryStream(FakePdf()), "std1", CancellationToken.None);
        afterGrade.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task OversizedDocument_IsRejected()
    {
        var internship = AddRunningInternship();
        var documents = new DocumentService(_context, new InMemoryFileStorage());

        var result = await documents.UploadAsync(internship.Id, InternshipDocumentKind.FinalThesis, "gros.pdf",
            InternshipDocument.MaxSizeBytes + 1, new MemoryStream(FakePdf()), "std1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("taille maximale");
    }

    [Fact]
    public async Task JuryMembers_Evaluate_ThenPresidentRecordsGrade_AndHeadCanCorrectIt()
    {
        var internship = AddRunningInternship();
        var identity = CreateIdentityMock(Teacher("prof1", "Professeur Un"), Teacher("prof2", "Professeur Deux"), Student("std1", "Etudiant Un", _track.Id));
        var clock = new TestClock(DateTime.UtcNow.AddHours(-3));
        var service = new DefenseService(_context, identity.Object, LenientDefensePolicy, clock);
        AddThesis(_context, internship.Id);

        var scheduled = await service.ScheduleDefenseAsync(new ScheduleDefenseCommand(internship.Id, DateTime.UtcNow.AddHours(-1), 45, "Salle A", new List<JuryMemberInput>
        {
            new("prof1", null, DefenseRole.President),
            new("prof2", null, DefenseRole.Examinateur)
        }), "head", CancellationToken.None);
        scheduled.IsSuccess.Should().BeTrue(scheduled.IsFailure ? scheduled.Error : string.Empty);
        var defenseId = scheduled.Value!.Id;
        clock.Now = DateTimeOffset.UtcNow;

        (await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(14, "Bien"), "prof1", CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(16, null), "prof2", CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await service.SubmitEvaluationAsync(defenseId, new SubmitEvaluationCommand(10, null), "intrus", CancellationToken.None)).IsFailure.Should().BeTrue();

        (await service.IsJuryPresidentAsync(defenseId, "prof1", CancellationToken.None)).Should().BeTrue();
        (await service.IsJuryPresidentAsync(defenseId, "prof2", CancellationToken.None)).Should().BeFalse();

        var detail = await service.GetDefenseByIdAsync(defenseId, CancellationToken.None);
        detail.Value!.AverageEvaluation.Should().Be(15);
        detail.Value.Evaluations.Should().HaveCount(2);

        detail.Value.ProvisionalGrade.Should().Be(15, "note calculée : moyenne des membres internes");

        (await service.RecordGradeAsync(defenseId, new RecordGradeCommand("Délibéré"), "prof1", CancellationToken.None)).IsSuccess.Should().BeTrue();
        _context.Defenses.Single(d => d.Id == defenseId).FinalGrade.Should().Be(15m);
        (await service.RecordGradeAsync(defenseId, new RecordGradeCommand(null), "prof1", CancellationToken.None)).IsFailure.Should().BeTrue();

        (await service.CorrectGradeAsync(defenseId, new CorrectGradeCommand(15.5m, null, "Erreur de report"), "head", CancellationToken.None)).IsSuccess.Should().BeTrue();
        _context.Defenses.Single(d => d.Id == defenseId).FinalGrade.Should().Be(15.5m);

        var forStudent = await service.GetAllDefensesAsync("std1", UserRole.Etudiant, null, CancellationToken.None);
        forStudent.Single().Evaluations.Should().BeEmpty("les évaluations individuelles restent confidentielles pour l'élève");
        forStudent.Single().FinalGrade.Should().Be(15.5m);
    }

    [Fact]
    public async Task TeachersForSupervision_AreFilteredByTrack_TransversalTeachersIncluded()
    {
        var gtr = Guid.NewGuid();
        var identity = CreateIdentityMock(
            Teacher("gi", "Prof GI") with { AcademicTrackId = _track.Id },
            Teacher("gtr", "Prof GTR") with { AcademicTrackId = gtr },
            Teacher("transverse", "Prof Transverse"));
        var service = new DefenseService(_context, identity.Object);

        var forGi = await service.GetTeachersAsync(_track.Id, CancellationToken.None);
        forGi.Select(t => t.Id).Should().BeEquivalentTo(new[] { "gi", "transverse" });

        var all = await service.GetTeachersAsync(null, CancellationToken.None);
        all.Should().HaveCount(3, "un jury peut accueillir des enseignants d'autres filières");
    }
}
