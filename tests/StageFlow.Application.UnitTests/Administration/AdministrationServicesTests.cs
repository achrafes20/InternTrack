using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Models;
using StageFlow.Application.Features.Anomalies;
using StageFlow.Application.Features.Conventions.Services;
using StageFlow.Application.Features.Dashboard.Services;
using StageFlow.Application.Features.Defenses.DTOs;
using StageFlow.Application.Features.Defenses.Services;
using StageFlow.Application.Features.Notifications;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Domain.Events;
using StageFlow.Infrastructure.Persistence;
using Xunit;
using static StageFlow.Application.UnitTests.TestHelpers;

namespace StageFlow.Application.UnitTests.Administration;

public class AdministrationServicesTests
{
    private readonly StageFlowDbContext _context = CreateInMemoryDbContext();
    private readonly AcademicTrack _track = new("GI", "Génie Info", "");
    private readonly Company _company = new("Tech Maroc", "IT", "Casablanca", "Addr", "tech@test.com");
    private readonly Mock<IEmailSender> _email = new();
    private readonly List<(string To, string Subject, string Body)> _sent = new();

    public AdministrationServicesTests()
    {
        _context.AddRange(_track, _company);
        _context.SaveChanges();
        _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string to, string subject, string body, CancellationToken _) => _sent.Add((to, subject, body)))
            .Returns(Task.CompletedTask);
    }

    private Mock<IIdentityService> Identity() => CreateIdentityMock(
        Student("std1", "Etudiant Un", _track.Id) with { StudentNumber = "E100", GraduationYear = 2027 },
        Student("std2", "Etudiant Deux", _track.Id),
        Teacher("prof1", "Professeur Un") with { AcademicTrackId = _track.Id },
        Teacher("head", "Responsable GI", UserRole.ResponsableFiliere) with { AcademicTrackId = _track.Id },
        new StageFlow.Application.Features.Auth.Models.UserDto("orphan", "orphan@ensa.ma", "Sans", "Filière", "Sans Filière", UserRole.Etudiant, "E9", null));

    private Internship Add(string student, Action<Internship> configure, DateTime? start = null)
    {
        var s = start ?? DateTime.UtcNow.Date.AddDays(-30);
        var internship = new Internship($"Stage {student} {Guid.NewGuid():N}"[..20], "Desc", s, s.AddDays(140), student, _company.Id, _track.Id, "Tuteur", "tuteur@tech.ma");
        configure(internship);
        _context.Internships.Add(internship);
        _context.SaveChanges();
        return internship;
    }

    private static void Running(Internship i) { i.SubmitForApproval(i.StudentId); i.Approve("head"); i.StartInternship("head"); }

    [Fact]
    public async Task Anomalies_DetectLegacyInconsistencies()
    {
        // Données « héritées » : deux dossiers engagés pour std1, dont un stage en cours sans convention ni encadrant.
        var running = Add("std1", Running);
        Add("std1", i => i.SubmitForApproval("std1"));
        // Stage validé dont le début est dépassé depuis longtemps.
        Add("std2", i => { i.SubmitForApproval("std2"); i.Approve("head"); }, DateTime.UtcNow.Date.AddDays(-40));

        var anomalies = await new AnomalyService(_context, Identity().Object, MoroccoTime).DetectAsync(null, CancellationToken.None);
        var codes = anomalies.Select(a => a.Code).ToList();

        codes.Should().Contain("MULTIPLE_ENGAGED");
        codes.Should().Contain("RUNNING_WITHOUT_SIGNED_CONVENTION");
        codes.Should().Contain("RUNNING_WITHOUT_SUPERVISOR");
        codes.Should().Contain("VALIDATED_NOT_STARTED");
        codes.Should().Contain("VALIDATED_WITHOUT_CONVENTION");
        codes.Should().Contain("USER_WITHOUT_TRACK");
        anomalies.First().Severity.Should().Be(AnomalySeverity.Error, "les erreurs sont listées en premier");
        anomalies.Single(a => a.Code == "MULTIPLE_ENGAGED").StudentName.Should().Be("Etudiant Un");

        var forOtherTrack = await new AnomalyService(_context, Identity().Object, MoroccoTime).DetectAsync(Guid.NewGuid(), CancellationToken.None);
        forOtherTrack.Should().BeEmpty("le rapport d'un responsable est limité à sa filière");
        running.Should().NotBeNull();
    }

    [Fact]
    public async Task Anomalies_DisappearOnceTheDuplicateIsCancelled()
    {
        Add("std1", i => i.SubmitForApproval("std1"));
        var duplicate = Add("std1", i => i.SubmitForApproval("std1"));
        var service = new AnomalyService(_context, Identity().Object, MoroccoTime);
        (await service.DetectAsync(_track.Id, CancellationToken.None)).Should().Contain(a => a.Code == "MULTIPLE_ENGAGED");

        duplicate.Cancel("admin", "Doublon").IsSuccess.Should().BeTrue();
        await _context.SaveChangesAsync();

        (await service.DetectAsync(_track.Id, CancellationToken.None)).Should().NotContain(a => a.Code == "MULTIPLE_ENGAGED");
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+212 600", "'+212 600")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Capgemini; Casablanca", "\"Capgemini; Casablanca\"")]
    [InlineData("Simple", "Simple")]
    [InlineData(null, "")]
    public void Csv_EscapesSeparatorsAndNeutralizesFormulas(string? input, string expected)
        => CsvBuilder.Escape(input).Should().Be(expected);

    [Fact]
    public async Task CsvExport_IsScopedFilteredByYear_AndIncludesPromotion()
    {
        Add("std1", Running, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Add("std2", i => i.SubmitForApproval("std2"), new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(_context, Identity().Object);

        var csv = await service.ExportCsvAsync("admin", UserRole.Admin, null, "2026-2027", CancellationToken.None);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().StartWith("Année universitaire;Promotion;Élève;Code Apogée");
        lines.Should().HaveCount(2, "en-tête + le seul stage de 2026-2027");
        lines[1].Should().StartWith("2026-2027;2027;Etudiant Un;E100");

        var studentCsv = await service.ExportCsvAsync("std2", UserRole.Etudiant, _track.Id, null, CancellationToken.None);
        studentCsv.Should().NotContain("Etudiant Un", "un élève n'exporte que ses propres dossiers");

        var stats = await service.GetDashboardStatsAsync("admin", UserRole.Admin, null, CancellationToken.None, "2026-2027");
        stats.TotalInternships.Should().Be(1);
        stats.YearlyStats!.Select(y => y.AcademicYear).Should().Equal("2026-2027", "2025-2026");
    }

    [Fact]
    public async Task TutorEvaluation_ByEmailedLink()
    {
        var internship = Add("std1", Running);
        var identity = Identity();
        var defenses = new DefenseService(_context, identity.Object);
        var scheduled = await defenses.ScheduleDefenseAsync(new ScheduleDefenseCommand(internship.Id, DateTime.UtcNow.AddHours(-1), 45, "Salle A", new List<JuryMemberInput>
        {
            new("prof1", null, DefenseRole.President),
            new(null, "M. Tuteur", DefenseRole.TuteurEntreprise)
        }), "head", CancellationToken.None);
        scheduled.IsSuccess.Should().BeTrue(scheduled.IsFailure ? scheduled.Error : "");

        var tutor = new TutorEvaluationService(_context, identity.Object, _email.Object, MoroccoTime, new ConfigurationBuilder().Build());
        (await tutor.SendLinkAsync(scheduled.Value!.Id, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var mail = _sent.Single(m => m.To == "tuteur@tech.ma");
        var token = Regex.Match(mail.Body, "/defense/evaluate/([0-9a-f]{64})").Groups[1].Value;

        var info = await tutor.GetInfoAsync(token, CancellationToken.None);
        info.Value!.TutorName.Should().Be("M. Tuteur");
        info.Value.HasStarted.Should().BeTrue();

        (await tutor.SubmitAsync(token, new SubmitEvaluationCommand(16, "Excellent travail"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var detail = await defenses.GetDefenseByIdAsync(scheduled.Value.Id, CancellationToken.None);
        detail.Value!.Evaluations.Should().ContainSingle(e => e.TeacherFullName == "M. Tuteur" && e.Grade == 16);

        (await defenses.RecordGradeAsync(scheduled.Value.Id, new RecordGradeCommand(16, null), "prof1", CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await tutor.GetInfoAsync(token, CancellationToken.None)).IsFailure.Should().BeTrue("le lien se ferme avec la note finale");
    }

    [Fact]
    public async Task CompanySignature_NotifiesTheTrackHeadsAndTheStudent()
    {
        var internship = Add("std1", i => { i.SubmitForApproval("std1"); i.Approve("head"); });
        var convention = Convention.CreateFor(internship.Id, DateTime.UtcNow);
        _context.Conventions.Add(convention);
        _context.SaveChanges();

        var handler = new ConventionCompanySignedNotificationHandler(_context, Identity().Object, _email.Object);
        await handler.HandleAsync(new ConventionCompanySignedEvent(convention.Id, internship.Id, "Mme Idrissi", DateTime.UtcNow), CancellationToken.None);

        _sent.Select(m => m.To).Should().BeEquivalentTo(new[] { "head@ensa.ma", "std1@ensa.ma" });
        _sent.Should().OnlyContain(m => m.Body.Contains("Mme Idrissi"));
    }

    [Fact]
    public async Task StudentReply_NotifiesTheSupervisor_AndSupervisorCommentNotifiesTheStudent()
    {
        var internship = Add("std1", Running);
        internship.AssignAcademicSupervisor("prof1");
        var report = new ProgressReport(internship.Id, 1, "S1", "Travail", "", "", "");
        _context.ProgressReports.Add(report);
        var reply = new ReportComment(report.Id, "std1", "Etudiant Un", "Merci, c'est corrigé.");
        var remark = new ReportComment(report.Id, "prof1", "Professeur Un", "Bien vu.");
        _context.ReportComments.AddRange(reply, remark);
        await _context.SaveChangesAsync();

        var handler = new ReportCommentAddedNotificationHandler(_context, Identity().Object, _email.Object);
        await handler.HandleAsync(new ReportCommentAddedEvent(reply.Id, report.Id, "std1", DateTime.UtcNow), CancellationToken.None);
        await handler.HandleAsync(new ReportCommentAddedEvent(remark.Id, report.Id, "prof1", DateTime.UtcNow), CancellationToken.None);

        _sent.Should().HaveCount(2);
        _sent[0].To.Should().Be("prof1@ensa.ma");
        _sent[0].Subject.Should().Contain("Réponse de l'élève");
        _sent[1].To.Should().Be("std1@ensa.ma");
    }

    [Fact]
    public async Task SignedConvention_IsFrozenOnce_AndEveryDownloadMatchesThePublishedFingerprint()
    {
        var internship = Add("std1", i => { i.SubmitForApproval("std1"); i.Approve("head"); });
        var convention = Convention.CreateFor(internship.Id, DateTime.UtcNow);
        convention.Sign(ConventionParty.Student);
        convention.Sign(ConventionParty.School);
        convention.RecordCompanySignature("Mme Idrissi", DateTime.UtcNow);
        _context.Conventions.Add(convention);
        _context.SaveChanges();

        var pdf = new Mock<IPdfGenerator>();
        pdf.Setup(p => p.GenerateConventionPdf(It.IsAny<ConventionPdfModel>()))
            .Returns(() => FakePdf(256).Concat(Guid.NewGuid().ToByteArray()).ToArray()); // chaque génération diffère
        var storage = new InMemoryFileStorage();
        var service = new ConventionService(_context, pdf.Object, Mock.Of<IQrCodeService>(), new ConfigurationBuilder().Build(),
            Identity().Object, _email.Object, MoroccoTime, Policy, storage);

        await new ConventionFullySignedHandler(service).HandleAsync(new ConventionFullySignedEvent(convention.Id, internship.Id, DateTime.UtcNow), CancellationToken.None);

        var first = await service.DownloadConventionPdfAsync(internship.Id, CancellationToken.None);
        var second = await service.DownloadConventionPdfAsync(internship.Id, CancellationToken.None);
        first.Value.Should().Equal(second.Value!, "l'exemplaire signé n'est jamais régénéré");

        var verification = await service.VerifyConventionAsync(convention.VerificationToken, CancellationToken.None);
        verification.Value!.SignedDocumentSha256.Should().Be(SecureToken.Fingerprint(first.Value!));
        storage.Files.Should().HaveCount(1);
    }
}
