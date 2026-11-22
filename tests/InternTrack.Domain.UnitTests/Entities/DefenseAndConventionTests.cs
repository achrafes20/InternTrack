using FluentAssertions;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Domain.Events;
using Xunit;

namespace InternTrack.Domain.UnitTests.Entities;

public class DefenseAndConventionTests
{
    [Fact]
    public void RecordGrade_OutOfRange_ShouldFail()
    {
        var defense = new Defense(Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), "Salle A");

        defense.RecordGrade(21, null, DateTime.UtcNow).IsFailure.Should().BeTrue();
        defense.RecordGrade(-1, null, DateTime.UtcNow).IsFailure.Should().BeTrue();
        defense.FinalGrade.Should().BeNull();
    }

    [Fact]
    public void RecordGrade_BeforeStart_ShouldFail()
    {
        var start = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var defense = new Defense(Guid.NewGuid(), start, "Salle A");

        defense.RecordGrade(15, null, start.AddMinutes(-1)).IsFailure.Should().BeTrue();
        defense.RecordGrade(15, "OK", start).IsSuccess.Should().BeTrue();
        defense.FinalGrade.Should().Be(15);
    }

    [Fact]
    public void Reschedule_ShouldUpdateDurationAndRaiseEvent()
    {
        var defense = new Defense(Guid.NewGuid(), DateTime.UtcNow, "Salle A");
        defense.ClearDomainEvents();

        defense.Reschedule(DateTime.UtcNow.AddDays(1), "Salle B", 90);

        defense.DurationMinutes.Should().Be(90);
        defense.Classroom.Should().Be("Salle B");
        defense.DomainEvents.Should().ContainSingle(e => e is DefenseScheduledEvent);
    }

    [Fact]
    public void Convention_ShouldOnlyBeFullySignedOnceAllPartiesSigned()
    {
        var convention = Convention.CreateFor(Guid.NewGuid(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        convention.ConventionNumber.Should().MatchRegex(@"^CONV-ENSA-2026-[0-9A-F]{6}$");
        convention.VerificationToken.Should().HaveLength(32);

        convention.Sign(ConventionParty.Student).IsSuccess.Should().BeTrue();
        convention.Sign(ConventionParty.School).IsSuccess.Should().BeTrue();
        convention.IsFullySigned.Should().BeFalse();

        convention.Sign(ConventionParty.School).IsFailure.Should().BeTrue();

        convention.Sign(ConventionParty.Company).IsSuccess.Should().BeTrue();
        convention.IsFullySigned.Should().BeTrue();
        convention.SignedAt.Should().NotBeNull();
    }

    [Fact]
    public void Internship_ReopenAsDraft_OnlyFromRefusedByOwner()
    {
        var internship = new Internship("Stage", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "std", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@test.com");

        internship.ReopenAsDraft("std").IsFailure.Should().BeTrue("un brouillon n'a pas à être réouvert");

        internship.SubmitForApproval("std");
        internship.Reject("head", "Motif");

        internship.ReopenAsDraft("autre").IsFailure.Should().BeTrue();
        internship.ReopenAsDraft("std").IsSuccess.Should().BeTrue();
        internship.Status.Should().Be(InternshipStatus.Brouillon);
        internship.RejectionReason.Should().Be("Motif", "le motif reste visible pour guider la correction");
    }

    [Fact]
    public void Internship_UpdateDraft_ShouldRejectInvalidData()
    {
        var internship = new Internship("Stage", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "std", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@test.com");

        internship.UpdateDraft("", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), "T", "t@t.com", null, "std")
            .IsFailure.Should().BeTrue();
        internship.UpdateDraft("Titre", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), Guid.NewGuid(), "T", "t@t.com", null, "std")
            .IsFailure.Should().BeTrue();
        internship.UpdateDraft("Titre", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), "T", "t@t.com", null, "autre")
            .IsFailure.Should().BeTrue();
    }
}
