using FluentAssertions;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using Xunit;

namespace InternTrack.Domain.UnitTests.Entities;

public class InternshipWorkflowTests
{
    private readonly string _studentId = "student-123";
    private readonly string _headId = "head-456";
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _trackId = Guid.NewGuid();

    private Internship CreateValidInternship()
    {
        return new Internship(
            title: "Stage .NET",
            subjectDescription: "Développement Web API",
            startDate: DateTime.UtcNow.AddDays(10),
            endDate: DateTime.UtcNow.AddDays(100),
            studentId: _studentId,
            companyId: _companyId,
            academicTrackId: _trackId,
            companySupervisorName: "Jean Dupont",
            companySupervisorEmail: "jean@tech.com"
        );
    }

    [Fact]
    public void NewInternship_ShouldHaveInitialStatusBrouillon()
    {
        var internship = CreateValidInternship();
        internship.Status.Should().Be(InternshipStatus.Brouillon);
    }

    [Fact]
    public void SubmitForApproval_ByOwner_ShouldTransitionToPropose()
    {
        var internship = CreateValidInternship();

        var result = internship.SubmitForApproval(_studentId);

        result.IsSuccess.Should().BeTrue();
        internship.Status.Should().Be(InternshipStatus.Propose);
    }

    [Fact]
    public void SubmitForApproval_ByNonOwner_ShouldFail()
    {
        var internship = CreateValidInternship();

        var result = internship.SubmitForApproval("other-student");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Seul l'étudiant propriétaire");
        internship.Status.Should().Be(InternshipStatus.Brouillon);
    }

    [Fact]
    public void Approve_WhenPropose_ShouldTransitionToValide()
    {
        var internship = CreateValidInternship();
        internship.SubmitForApproval(_studentId);

        var result = internship.Approve(_headId);

        result.IsSuccess.Should().BeTrue();
        internship.Status.Should().Be(InternshipStatus.Valide);
    }

    [Fact]
    public void Approve_WhenStillBrouillon_ShouldFail()
    {
        var internship = CreateValidInternship();

        var result = internship.Approve(_headId);

        result.IsFailure.Should().BeTrue();
        internship.Status.Should().Be(InternshipStatus.Brouillon);
    }

    [Fact]
    public void Reject_WithoutReason_ShouldFail()
    {
        var internship = CreateValidInternship();
        internship.SubmitForApproval(_studentId);

        var result = internship.Reject(_headId, "");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("motif explicite est obligatoire");
        internship.Status.Should().Be(InternshipStatus.Propose);
    }

    [Fact]
    public void Reject_WithReason_ShouldTransitionToRefuse()
    {
        var internship = CreateValidInternship();
        internship.SubmitForApproval(_studentId);

        var result = internship.Reject(_headId, "Sujet non conforme aux exigences du PFE.");

        result.IsSuccess.Should().BeTrue();
        internship.Status.Should().Be(InternshipStatus.Refuse);
        internship.RejectionReason.Should().Be("Sujet non conforme aux exigences du PFE.");
    }
}

