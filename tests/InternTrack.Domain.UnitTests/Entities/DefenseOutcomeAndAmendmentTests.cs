using FluentAssertions;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using Xunit;

namespace InternTrack.Domain.UnitTests.Entities;

public class DefenseOutcomeAndAmendmentTests
{
    private static readonly DateTime Start = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime After = Start.AddHours(1);

    private static Defense DefenseWithJury(bool withTutor = false)
    {
        var defense = new Defense(Guid.NewGuid(), Start, "Salle A");
        defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "president", "Pr. Alami", DefenseRole.President));
        defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "examiner", "Dr. Tazi", DefenseRole.Examinateur));
        if (withTutor)
            defense.JuryMembers.Add(new DefenseJuryMember(defense.Id, "external:tutor", "M. Tuteur", DefenseRole.TuteurEntreprise));
        return defense;
    }

    private static Internship ScheduledInternship()
    {
        var internship = new Internship("Stage", "Desc", Start.AddMonths(-4), Start, "std", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@x.ma");
        internship.SubmitForApproval("std");
        internship.Approve("head");
        internship.StartInternship("head");
        internship.MarkDefenseScheduled("head");
        return internship;
    }

    [Fact]
    public void FinalGrade_RequiresEveryInternalEvaluation_AndWeightsTheTutor()
    {
        var defense = DefenseWithJury(withTutor: true);
        defense.SubmitEvaluation("president", 12, null, After);

        defense.ComputeFinalGrade(0.25m, requireTutorEvaluation: false).IsFailure.Should().BeTrue("l'examinateur n'a pas évalué");

        defense.SubmitEvaluation("examiner", 14, null, After);
        defense.ComputeFinalGrade(0.25m, false).Value.Should().Be(13m, "sans évaluation du tuteur : moyenne du jury interne");
        defense.ComputeFinalGrade(0.25m, requireTutorEvaluation: true).IsFailure.Should().BeTrue();

        defense.SubmitEvaluation("external:tutor", 17, null, After);
        defense.ComputeFinalGrade(0.25m, false).Value.Should().Be(14m, "0,75 × 13 + 0,25 × 17");
    }

    [Theory]
    [InlineData(true, true, InternshipStatus.Termine)]
    [InlineData(false, true, InternshipStatus.Ajourne)]
    [InlineData(false, false, InternshipStatus.NonValide)]
    public void DefenseOutcome_FollowsThePassingThreshold(bool passed, bool retakeAllowed, InternshipStatus expected)
    {
        var internship = ScheduledInternship();

        internship.RecordDefenseOutcome(passed, retakeAllowed, "head").IsSuccess.Should().BeTrue();

        internship.Status.Should().Be(expected);
    }

    [Fact]
    public void Retake_KeepsThePreviousGrade_AndIsLimited()
    {
        var defense = DefenseWithJury();
        defense.SubmitEvaluation("president", 8, null, After);
        defense.SubmitEvaluation("examiner", 9, null, After);
        defense.RecordGrade(8.5m, null, After, "president");

        defense.StartRetake(Start.AddDays(30), "Salle B", 45, maxAttempts: 2).IsSuccess.Should().BeTrue();

        defense.Attempt.Should().Be(2);
        defense.PreviousAttemptGrade.Should().Be(8.5m);
        defense.FinalGrade.Should().BeNull();
        defense.Evaluations.Should().BeEmpty();

        defense.RecordGrade(7, null, Start.AddDays(30).AddHours(1), "president");
        defense.StartRetake(Start.AddDays(60), "Salle B", 45, maxAttempts: 2).IsFailure.Should().BeTrue("une seule session de rattrapage");
    }

    [Fact]
    public void CancelledDefense_CannotBeGraded_AndIsReactivatedWhenRescheduled()
    {
        var defense = DefenseWithJury();

        defense.Cancel(" ", After).IsFailure.Should().BeTrue("motif obligatoire");
        defense.Cancel("Absence justifiée", After).IsSuccess.Should().BeTrue();
        defense.SubmitEvaluation("president", 12, null, After).IsFailure.Should().BeTrue();
        defense.RecordGrade(12, null, After, "head").IsFailure.Should().BeTrue();

        defense.Reschedule(Start.AddDays(7), "Salle A", 45).IsSuccess.Should().BeTrue();
        defense.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void GradedDefense_CannotBeCancelledNorRescheduled()
    {
        var defense = DefenseWithJury();
        defense.RecordGrade(14, null, After, "head");

        defense.Cancel("Erreur", After).IsFailure.Should().BeTrue();
        defense.Reschedule(Start.AddDays(7), "Salle A", 45).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ScheduledInternship_CanBeCancelled_ButNotAFinalOutcome()
    {
        var scheduled = ScheduledInternship();
        scheduled.Cancel("head", "Abandon de l'élève").IsSuccess.Should().BeTrue();

        var failed = ScheduledInternship();
        failed.RecordDefenseOutcome(passed: false, retakeAllowed: false, "head");
        failed.Cancel("head", "Abandon").IsFailure.Should().BeTrue("un échec définitif est une décision du jury");
    }

    [Fact]
    public void TerminatedConvention_CannotBeSigned_AndWithdrawsPendingAmendments()
    {
        var convention = Convention.CreateFor(Guid.NewGuid(), Start);
        convention.SignByStudent();
        convention.SignBySchool();
        convention.RecordCompanySignature("Directeur", Start);
        var amendment = new ConventionAmendment(convention.Id, 1, "Prolongation", "head", null, Start.AddMonths(1), null, null, null, null, null, "Date de fin");
        convention.Amendments.Add(amendment);

        convention.Terminate("Dossier annulé", Start).IsSuccess.Should().BeTrue();

        convention.IsTerminated.Should().BeTrue();
        amendment.IsPending.Should().BeFalse();
        convention.IssueCompanySigningToken(Start, TimeSpan.FromDays(7)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Amendment_IsAppliedOnlyOnceSignedByTheThreeParties()
    {
        var amendment = new ConventionAmendment(Guid.NewGuid(), 1, "Prolongation", "head", null, Start.AddMonths(1), null, null, "Nouveau Tuteur", null, null, "…");
        amendment.Sign(ConventionParty.School);
        amendment.Sign(ConventionParty.Student);

        amendment.MarkApplied(Start).IsFailure.Should().BeTrue("l'entreprise n'a pas signé");
        amendment.Sign(ConventionParty.Company).IsFailure.Should().BeTrue("nom du signataire obligatoire");
        amendment.Sign(ConventionParty.Company, "Directeur RH").IsSuccess.Should().BeTrue();

        amendment.MarkApplied(Start).IsSuccess.Should().BeTrue();
        amendment.Withdraw("Erreur", Start).IsFailure.Should().BeTrue("un avenant appliqué ne se retire pas");
    }

    [Fact]
    public void ApplyAmendment_ChangesTheFile_ButNotTheStartDateOfARunningInternship()
    {
        var internship = new Internship("Stage", "Desc", Start, Start.AddMonths(4), "std", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@x.ma");
        internship.SubmitForApproval("std");
        internship.Approve("head");
        internship.StartInternship("head");

        internship.ApplyAmendment(Start.AddDays(7), null, null, null, null, null, null).IsFailure.Should().BeTrue();

        var newEnd = Start.AddMonths(5);
        internship.ApplyAmendment(null, newEnd, null, null, "Nouveau Tuteur", "nouveau@x.ma", "").IsSuccess.Should().BeTrue();
        internship.Period.EndDate.Should().Be(newEnd);
        internship.CompanySupervisorName.Should().Be("Nouveau Tuteur");
        internship.CompanySupervisorPhone.Should().BeNull();
    }
}
