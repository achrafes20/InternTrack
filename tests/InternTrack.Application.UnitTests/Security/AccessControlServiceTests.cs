using FluentAssertions;
using Moq;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Common.Security;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Infrastructure.Persistence;
using Xunit;
using static InternTrack.Application.UnitTests.TestHelpers;

namespace InternTrack.Application.UnitTests.Security;

public class AccessControlServiceTests
{
    private readonly InternTrackDbContext _context = CreateInMemoryDbContext();
    private readonly Guid _giTrackId;
    private readonly Guid _gtrTrackId;
    private readonly Internship _internship;
    private readonly Defense _defense;

    public AccessControlServiceTests()
    {
        var gi = new AcademicTrack("GI", "Génie Info", "");
        var gtr = new AcademicTrack("GTR", "Télécoms", "");
        var company = new Company("Tech", "IT", "Casablanca", "Addr", "tech@test.com");
        _context.AddRange(gi, gtr, company);

        _internship = CreateInProgressInternship("student-1", company.Id, gi.Id);
        _internship.AssignAcademicSupervisor("supervisor-1");
        _context.Internships.Add(_internship);

        _defense = new Defense(_internship.Id, DateTime.UtcNow.AddDays(2), "Salle A");
        _context.Defenses.Add(_defense);
        _context.DefenseJuryMembers.Add(new DefenseJuryMember(_defense.Id, "jury-teacher", "Jury Prof", DefenseRole.President));
        _context.SaveChanges();

        _giTrackId = gi.Id;
        _gtrTrackId = gtr.Id;
    }

    private AccessControlService As(string? userId, string role, Guid? trackId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.UserRole).Returns(role);
        currentUser.SetupGet(c => c.AcademicTrackId).Returns(trackId?.ToString());
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(userId != null);
        return new AccessControlService(_context, currentUser.Object);
    }

    [Fact]
    public async Task Student_CanAccessOwnInternship_ButNotOthers()
    {
        (await As("student-1", UserRole.Etudiant, _giTrackId).CanAccessInternshipAsync(_internship.Id)).Should().BeTrue();
        (await As("student-2", UserRole.Etudiant, _giTrackId).CanAccessInternshipAsync(_internship.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Student_CanNeverManageInternship()
    {
        (await As("student-1", UserRole.Etudiant, _giTrackId).CanManageInternshipAsync(_internship.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Supervisor_CanOnlyAccessSupervisedInternships()
    {
        (await As("supervisor-1", UserRole.Encadrant).CanAccessInternshipAsync(_internship.Id)).Should().BeTrue();
        (await As("supervisor-2", UserRole.Encadrant).CanAccessInternshipAsync(_internship.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DepartmentHead_IsLimitedToOwnTrack()
    {
        (await As("head-gi", UserRole.ResponsableFiliere, _giTrackId).CanManageInternshipAsync(_internship.Id)).Should().BeTrue();
        (await As("head-gtr", UserRole.ResponsableFiliere, _gtrTrackId).CanManageInternshipAsync(_internship.Id)).Should().BeFalse();
        (await As("head-none", UserRole.ResponsableFiliere).CanAccessInternshipAsync(_internship.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_CanAccessAndManageEverything()
    {
        var admin = As("admin", UserRole.Admin);
        (await admin.CanAccessInternshipAsync(_internship.Id)).Should().BeTrue();
        (await admin.CanManageInternshipAsync(_internship.Id)).Should().BeTrue();
        (await admin.CanManageDefenseAsync(_defense.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task JuryMember_CanAccessDefense_ButNotManageIt()
    {
        var juryTeacher = As("jury-teacher", UserRole.Encadrant);
        (await juryTeacher.CanAccessDefenseAsync(_defense.Id)).Should().BeTrue();
        (await juryTeacher.CanManageDefenseAsync(_defense.Id)).Should().BeFalse();
        (await As("random-teacher", UserRole.Encadrant).CanAccessDefenseAsync(_defense.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Anonymous_IsAlwaysDenied()
    {
        var anonymous = As(null, string.Empty);
        (await anonymous.CanAccessInternshipAsync(_internship.Id)).Should().BeFalse();
        (await anonymous.CanAccessDefenseAsync(_defense.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownResource_IsDenied()
    {
        (await As("head-gi", UserRole.ResponsableFiliere, _giTrackId).CanManageInternshipAsync(Guid.NewGuid())).Should().BeFalse();
    }
}
