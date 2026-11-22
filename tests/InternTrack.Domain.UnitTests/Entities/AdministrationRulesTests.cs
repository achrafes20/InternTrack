using FluentAssertions;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Domain.Events;
using Xunit;

namespace InternTrack.Domain.UnitTests.Entities;

public class AdministrationRulesTests
{
    private static Internship NewInternship(DateTime start) =>
        new("Stage", "Desc", start, start.AddMonths(5), "std", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@t.com");

    [Theory]
    [InlineData(2026, 9, 1, "2026-2027")]
    [InlineData(2026, 8, 31, "2025-2026")]
    [InlineData(2027, 2, 15, "2026-2027")]
    [InlineData(2026, 12, 31, "2026-2027")]
    public void AcademicYear_StartsOnSeptemberFirst(int y, int m, int d, string expected)
    {
        Internship.ComputeAcademicYear(new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc)).Should().Be(expected);
        NewInternship(new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc)).AcademicYear.Should().Be(expected);
    }

    [Fact]
    public void AcademicYear_FollowsThePeriodWhenTheDraftIsEdited()
    {
        var internship = NewInternship(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        internship.AcademicYear.Should().Be("2025-2026");

        internship.UpdateDraft("Stage", "Desc", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            internship.CompanyId, "Tuteur", "t@t.com", null, "std").IsSuccess.Should().BeTrue();

        internship.AcademicYear.Should().Be("2026-2027");
    }

    [Fact]
    public void Cancel_RequiresAReason_AndIsForbiddenOnceTheDefenseIsScheduled()
    {
        var internship = NewInternship(DateTime.UtcNow);
        internship.SubmitForApproval("std");

        internship.Cancel("admin", " ").IsFailure.Should().BeTrue();
        internship.Cancel("admin", "Doublon de dossier").IsSuccess.Should().BeTrue();
        internship.Status.Should().Be(InternshipStatus.Annule);
        internship.CancellationReason.Should().Be("Doublon de dossier");
        internship.Cancel("admin", "Encore").IsFailure.Should().BeTrue("état final");

        var scheduled = NewInternship(DateTime.UtcNow);
        scheduled.SubmitForApproval("std");
        scheduled.Approve("head");
        scheduled.StartInternship("head");
        scheduled.MarkDefenseScheduled("head");
        scheduled.Cancel("admin", "Abandon").IsFailure.Should().BeTrue("une soutenance planifiée relève d'une décision du jury");
    }

    [Fact]
    public void CancelledInternship_NoLongerCountsAsEngaged()
    {
        Internship.EngagedStatuses.Should().NotContain(InternshipStatus.Annule);
    }

    [Fact]
    public void TutorEvaluationLink_RequiresATutorInTheJury_AndClosesWithTheFinalGrade()
    {
        var start = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var withoutTutor = new Defense(Guid.NewGuid(), start, "Salle A");
        withoutTutor.JuryMembers.Add(new DefenseJuryMember(withoutTutor.Id, "p1", "Pr. Un", DefenseRole.President));
        withoutTutor.IssueTutorEvaluationToken(start, TimeSpan.FromDays(30)).IsFailure.Should().BeTrue();

        var defense = new Defense(Guid.NewGuid(), start, "Salle A");
        defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "p1", "Pr. Un", DefenseRole.President));
        defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "external:x", "M. Tuteur", DefenseRole.TuteurEntreprise));

        var token = defense.IssueTutorEvaluationToken(start.AddDays(-2), TimeSpan.FromDays(30)).Value!;
        var after = start.AddHours(1);

        defense.SubmitTutorEvaluation("mauvais", 14, null, after).IsFailure.Should().BeTrue();
        defense.SubmitTutorEvaluation(token, 14, null, start.AddMinutes(-5)).IsFailure.Should().BeTrue("pas avant le début");
        defense.SubmitTutorEvaluation(token, 14, "Très investi", after).IsSuccess.Should().BeTrue();
        defense.SubmitTutorEvaluation(token, 15, "Très investi", after).IsSuccess.Should().BeTrue("révisable");
        defense.Evaluations.Should().ContainSingle(e => e.TeacherId == "external:x" && e.Grade == 15);

        defense.RecordGrade(15, null, after, "p1").IsSuccess.Should().BeTrue();
        defense.IsTutorEvaluationTokenValid(token, after).Should().BeFalse("le lien se ferme avec la note finale");
    }

    [Fact]
    public void Convention_RaisesEvents_AndCanBeFrozenOnlyOnceWhenFullySigned()
    {
        var convention = Convention.CreateFor(Guid.NewGuid(), DateTime.UtcNow);
        convention.FreezeSignedDocument("k", "h").IsFailure.Should().BeTrue("non signée");

        convention.Sign(ConventionParty.Student);
        convention.Sign(ConventionParty.School);
        convention.RecordCompanySignature("Mme Idrissi", DateTime.UtcNow).IsSuccess.Should().BeTrue();

        convention.DomainEvents.Should().Contain(e => e is ConventionCompanySignedEvent);
        convention.DomainEvents.Should().ContainSingle(e => e is ConventionFullySignedEvent);

        convention.FreezeSignedDocument("2026/11/doc.pdf", new string('a', 64)).IsSuccess.Should().BeTrue();
        convention.FreezeSignedDocument("autre.pdf", new string('b', 64)).IsFailure.Should().BeTrue("un exemplaire figé ne change plus");
        convention.SignedDocumentSha256.Should().Be(new string('a', 64));
    }

    [Fact]
    public void Fingerprint_IsStableAndSensitiveToAnyChange()
    {
        var original = "%PDF-1.7 contenu"u8.ToArray();
        var altered = "%PDF-1.7 contenU"u8.ToArray();

        SecureToken.Fingerprint(original).Should().Be(SecureToken.Fingerprint(original.ToArray()));
        SecureToken.Fingerprint(original).Should().NotBe(SecureToken.Fingerprint(altered));
        SecureToken.Fingerprint(original).Should().HaveLength(64);
    }

    [Fact]
    public void ReportComment_RaisesAnEventForNotifications()
    {
        var comment = new ReportComment(Guid.NewGuid(), "std", "Élève", "Merci pour vos remarques");
        comment.DomainEvents.Should().ContainSingle(e => e is ReportCommentAddedEvent);
    }
}
