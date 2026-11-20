using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Conventions.Services;
using StageFlow.Application.Features.Internships.DTOs;
using StageFlow.Application.Features.Internships.Services;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Persistence;
using Xunit;
using static StageFlow.Application.UnitTests.TestHelpers;

namespace StageFlow.Application.UnitTests.Internships;

public class InternshipServiceTests
{
    private readonly StageFlowDbContext _context = CreateInMemoryDbContext();
    private readonly InternshipService _service;
    private readonly Company _company;
    private readonly AcademicTrack _track;

    public InternshipServiceTests()
    {
        _track = new AcademicTrack("GI", "Génie Info", "");
        _company = new Company("Tech", "IT", "Casablanca", "Addr", "tech@test.com");
        _context.AddRange(_track, _company);
        _context.SaveChanges();

        var identity = CreateIdentityMock(Student("std1", "Etudiant Un", _track.Id), Teacher("prof1", "Professeur Un"));
        _service = new InternshipService(_context, identity.Object);
    }

    private Internship AddProposedInternship()
    {
        var internship = new Internship("Stage", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(4), "std1", _company.Id, _track.Id, "Tuteur", "t@test.com");
        internship.SubmitForApproval("std1");
        _context.Internships.Add(internship);
        _context.SaveChanges();
        return internship;
    }

    [Fact]
    public async Task Approve_ShouldCreateASingleUnsignedConvention()
    {
        var internship = AddProposedInternship();

        var result = await _service.ApproveAsync(internship.Id, "head", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var convention = _context.Conventions.Single(c => c.InternshipId == internship.Id);
        convention.ConventionNumber.Should().StartWith($"CONV-ENSA-{DateTime.UtcNow.Year}-");
        convention.IsFullySigned.Should().BeFalse();
    }

    [Fact]
    public async Task RejectedInternship_CanBeReopenedEditedAndResubmitted()
    {
        var internship = AddProposedInternship();
        (await _service.RejectAsync(internship.Id, "head", "Sujet trop léger", CancellationToken.None)).IsSuccess.Should().BeTrue();

        (await _service.ReopenAsync(internship.Id, "std1", CancellationToken.None)).IsSuccess.Should().BeTrue();

        var update = await _service.UpdateDraftAsync(internship.Id, new UpdateInternshipDraftCommand
        {
            Title = "Stage enrichi",
            SubjectDescription = "Description détaillée",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(5),
            CompanyId = _company.Id,
            CompanySupervisorName = "Tuteur",
            CompanySupervisorEmail = "t@test.com"
        }, "std1", CancellationToken.None);
        update.IsSuccess.Should().BeTrue(update.IsFailure ? update.Error : string.Empty);

        (await _service.SubmitAsync(internship.Id, "std1", CancellationToken.None)).IsSuccess.Should().BeTrue();

        var stored = _context.Internships.Single(i => i.Id == internship.Id);
        stored.Status.Should().Be(InternshipStatus.Propose);
        stored.Title.Should().Be("Stage enrichi");
    }

    [Fact]
    public async Task Reopen_ByAnotherStudent_ShouldFail()
    {
        var internship = AddProposedInternship();
        await _service.RejectAsync(internship.Id, "head", "Motif", CancellationToken.None);

        var result = await _service.ReopenAsync(internship.Id, "intrus", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_ShouldResolveStudentAndSupervisorNames()
    {
        var internship = AddProposedInternship();
        await _service.AssignSupervisorAsync(internship.Id, "prof1", CancellationToken.None);

        var list = await _service.GetAllAsync(null, null, null, null, CancellationToken.None);

        list.Single().StudentName.Should().Be("Etudiant Un");
        list.Single().AcademicSupervisorName.Should().Be("Professeur Un");
    }

    [Fact]
    public async Task ConventionIsFullySigned_OnlyAfterAllThreeParties()
    {
        var internship = AddProposedInternship();
        await _service.ApproveAsync(internship.Id, "head", CancellationToken.None);

        var conventions = new ConventionService(
            _context,
            Mock.Of<IPdfGenerator>(),
            Mock.Of<IQrCodeService>(),
            new ConfigurationBuilder().Build(),
            CreateIdentityMock().Object);

        (await conventions.SignAsync(internship.Id, ConventionParty.Student, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await conventions.SignAsync(internship.Id, ConventionParty.Student, CancellationToken.None)).IsFailure.Should().BeTrue("une partie ne signe qu'une fois");
        (await conventions.SignAsync(internship.Id, ConventionParty.Company, CancellationToken.None)).IsSuccess.Should().BeTrue();

        _context.Conventions.Single().IsFullySigned.Should().BeFalse();

        (await conventions.SignAsync(internship.Id, ConventionParty.School, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var token = _context.Conventions.Single().VerificationToken;
        var verification = await conventions.VerifyConventionAsync(token, CancellationToken.None);
        verification.Value!.IsFullySigned.Should().BeTrue();
        verification.Value.IsSignedByCompany.Should().BeTrue();
    }

    [Fact]
    public async Task StartInternship_RequiresAFullySignedConvention()
    {
        var internship = AddProposedInternship();
        await _service.ApproveAsync(internship.Id, "head", CancellationToken.None);

        var tooEarly = await _service.StartInternshipAsync(internship.Id, "head", CancellationToken.None);
        tooEarly.IsFailure.Should().BeTrue();
        tooEarly.Error.Should().Contain("signature de la convention");

        var convention = _context.Conventions.Single(c => c.InternshipId == internship.Id);
        convention.Sign(ConventionParty.Student);
        convention.Sign(ConventionParty.Company);
        convention.Sign(ConventionParty.School);
        await _context.SaveChangesAsync();

        (await _service.StartInternshipAsync(internship.Id, "head", CancellationToken.None)).IsSuccess.Should().BeTrue();
        _context.Internships.Single(i => i.Id == internship.Id).Status.Should().Be(InternshipStatus.EnCours);
    }

    [Fact]
    public async Task DownloadPdf_WithoutConvention_ShouldFailWithoutCreatingOne()
    {
        var internship = AddProposedInternship();
        var conventions = new ConventionService(
            _context,
            Mock.Of<IPdfGenerator>(),
            Mock.Of<IQrCodeService>(),
            new ConfigurationBuilder().Build(),
            CreateIdentityMock().Object);

        var result = await conventions.DownloadConventionPdfAsync(internship.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _context.Conventions.Should().BeEmpty();
    }
}
