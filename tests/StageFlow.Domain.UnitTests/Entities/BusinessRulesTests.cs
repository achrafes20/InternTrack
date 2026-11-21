using FluentAssertions;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using Xunit;

namespace StageFlow.Domain.UnitTests.Entities;

public class BusinessRulesTests
{
    private static readonly DateTime Start = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Defense DefenseWithJury()
    {
        var defense = new Defense(Guid.NewGuid(), Start, "Salle A");
        defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "president", "Pr. Alami", DefenseRole.President));
        defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "examiner", "Dr. Tazi", DefenseRole.Examinateur));
        return defense;
    }

    [Fact]
    public void JuryEvaluation_OnlyByMembers_RevisableUntilFinalGrade_AndAveraged()
    {
        var defense = DefenseWithJury();
        var after = Start.AddHours(1);

        defense.SubmitEvaluation("outsider", 12, null, after).IsFailure.Should().BeTrue();
        defense.SubmitEvaluation("president", 14, "Bien", Start.AddMinutes(-1)).IsFailure.Should().BeTrue("pas avant le début");

        defense.SubmitEvaluation("president", 14, "Bien", after).IsSuccess.Should().BeTrue();
        defense.SubmitEvaluation("examiner", 15.5m, null, after).IsSuccess.Should().BeTrue();
        defense.SubmitEvaluation("president", 16, "Très bien", after).IsSuccess.Should().BeTrue("révision de sa propre évaluation");

        defense.Evaluations.Should().HaveCount(2);
        defense.AverageEvaluation.Should().Be(15.75m);

        defense.RecordGrade(15.75m, "Délibéré", after, "president").IsSuccess.Should().BeTrue();
        defense.SubmitEvaluation("examiner", 10, null, after).IsFailure.Should().BeTrue("la note est arrêtée");
    }

    [Fact]
    public void Grade_CannotBeRecordedTwice_ButCanBeCorrectedWithAReason()
    {
        var defense = DefenseWithJury();
        var after = Start.AddHours(1);
        defense.RecordGrade(12, null, after, "head").IsSuccess.Should().BeTrue();

        defense.RecordGrade(13, null, after, "head").IsFailure.Should().BeTrue();
        defense.CorrectGrade(13, null, "  ", after, "head").IsFailure.Should().BeTrue("motif obligatoire");

        defense.CorrectGrade(13.5m, "Erreur de report", "Erreur de saisie corrigée après délibération", after, "head").IsSuccess.Should().BeTrue();
        defense.FinalGrade.Should().Be(13.5m);
        defense.GradeCorrectionReason.Should().Be("Erreur de saisie corrigée après délibération");
        defense.IsPresident("president").Should().BeTrue();
    }

    [Fact]
    public void CompanySigningLink_IsSingleUse_AndExpires()
    {
        var now = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var convention = Convention.CreateFor(Guid.NewGuid(), now);

        var token = convention.IssueCompanySigningToken(now, TimeSpan.FromDays(14)).Value!;
        convention.CompanySigningTokenHash.Should().NotBe(token, "seul le hash est conservé");

        convention.SignByCompanyWithToken("mauvais-jeton", "X", now).IsFailure.Should().BeTrue();
        convention.SignByCompanyWithToken(token, "X", now.AddDays(15)).IsFailure.Should().BeTrue("lien expiré");

        convention.SignByCompanyWithToken(token, "Mme Idrissi, DRH", now.AddDays(1)).IsSuccess.Should().BeTrue();
        convention.IsSignedByCompany.Should().BeTrue();
        convention.CompanySignerName.Should().Be("Mme Idrissi, DRH");
        convention.CompanySigningTokenHash.Should().BeNull("le lien ne peut servir qu'une fois");
        convention.IssueCompanySigningToken(now, TimeSpan.FromDays(1)).IsFailure.Should().BeTrue("déjà signée");
    }

    [Fact]
    public void Reissuing_ALink_InvalidatesThePreviousOne()
    {
        var now = DateTime.UtcNow;
        var convention = Convention.CreateFor(Guid.NewGuid(), now);
        var first = convention.IssueCompanySigningToken(now, TimeSpan.FromDays(14)).Value!;
        var second = convention.IssueCompanySigningToken(now, TimeSpan.FromDays(14)).Value!;

        convention.IsCompanySigningTokenValid(first, now).Should().BeFalse();
        convention.IsCompanySigningTokenValid(second, now).Should().BeTrue();
    }

    [Fact]
    public void ProgressReport_CanBeEdited_UntilValidated_AndEditingClearsTheAiSummary()
    {
        var report = new ProgressReport(Guid.NewGuid(), 2, "S2", "Activités", "", "", "");
        report.SetAiSummary("Résumé");

        report.Update("", "Activités corrigées", "C#", "", "Suite").IsSuccess.Should().BeTrue();
        report.Title.Should().Be("Rapport Semaine 2");
        report.AiSummary.Should().BeNull();
        report.Update("T", "  ", "", "", "").IsFailure.Should().BeTrue();

        report.MarkAsValidated();
        report.Update("T", "Encore", "", "", "").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AiSummary_IsTruncatedToItsColumnSize()
    {
        var report = new ProgressReport(Guid.NewGuid(), 1, "S1", "Activités", "", "", "");
        report.SetAiSummary(new string('a', 5000));
        report.AiSummary!.Length.Should().Be(3000);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd.pdf")]
    [InlineData("C:\\Users\\x\\mémoire final.pdf", "mémoire final.pdf")]
    [InlineData("", "document.pdf")]
    [InlineData("rapport\"<script>.PDF", "rapport<script>.PDF")]
    public void DocumentFileName_IsSanitized(string input, string expected)
        => InternshipDocument.SanitizeFileName(input).Should().Be(expected);

    [Fact]
    public void Internship_WeeksAreDerivedFromThePeriod()
    {
        var internship = new Internship("Stage", "Desc", Start, Start.AddDays(7 * 20 + 1), "std", Guid.NewGuid(), Guid.NewGuid(), "T", "t@t.com");

        internship.TotalWeeks.Should().Be(21, "la semaine entamée compte");
        internship.WeekStart(3).Should().Be(Start.AddDays(14));
    }
}
