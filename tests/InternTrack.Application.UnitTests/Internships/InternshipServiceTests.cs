using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Features.Conventions.DTOs;
using InternTrack.Application.Features.Conventions.Services;
using InternTrack.Application.Features.Documents;
using InternTrack.Application.Features.Internships.DTOs;
using InternTrack.Application.Features.Internships.Services;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Infrastructure.Persistence;
using Xunit;
using static InternTrack.Application.UnitTests.TestHelpers;

namespace InternTrack.Application.UnitTests.Internships;

public class InternshipServiceTests
{
    private readonly InternTrackDbContext _context = CreateInMemoryDbContext();
    private readonly InternshipService _service;
    private readonly Company _company;
    private readonly AcademicTrack _track;
    private readonly AcademicTrack _otherTrack;
    private readonly Mock<IEmailSender> _emailSender = new();

    public InternshipServiceTests()
    {
        _track = new AcademicTrack("GI", "Génie Info", "");
        _otherTrack = new AcademicTrack("GTR", "Télécoms", "");
        _company = new Company("Tech", "IT", "Casablanca", "Addr", "tech@test.com");
        _context.AddRange(_track, _otherTrack, _company);
        _context.SaveChanges();

        var otherTrackTeacher = Teacher("prof-gtr", "Professeur Télécoms") with { AcademicTrackId = _otherTrack.Id };
        var identity = CreateIdentityMock(Student("std1", "Etudiant Un", _track.Id), Teacher("prof1", "Professeur Un"), otherTrackTeacher);
        _service = new InternshipService(_context, identity.Object, Policy, MoroccoTime);
    }

    private readonly InMemoryFileStorage _storage = new();

    private ConventionService CreateConventionService()
    {
        // Chaque génération produit un PDF différent (horodatage) : on le simule avec un contenu unique.
        var pdf = new Mock<IPdfGenerator>();
        pdf.Setup(p => p.GenerateConventionPdf(It.IsAny<ConventionPdfModel>()))
            .Returns(() => FakePdf(512).Concat(Guid.NewGuid().ToByteArray()).ToArray());

        return new ConventionService(
            _context,
            pdf.Object,
            Mock.Of<IQrCodeService>(),
            new ConfigurationBuilder().Build(),
            CreateIdentityMock().Object,
            _emailSender.Object,
            MoroccoTime,
            Policy,
            _storage);
    }

    private Internship AddProposedInternship(string studentId = "std1")
    {
        var internship = new Internship("Stage", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(4), studentId, _company.Id, _track.Id, "Tuteur", "t@test.com");
        internship.SubmitForApproval(studentId);
        _context.Internships.Add(internship);
        _context.SaveChanges();
        return internship;
    }

    private ProposeInternshipCommand ValidProposal(DateTime? start = null, DateTime? end = null, Guid? offerId = null) => new()
    {
        Title = "Nouveau stage",
        SubjectDescription = "Sujet",
        StartDate = start ?? DateTime.UtcNow.Date.AddDays(14),
        EndDate = end ?? DateTime.UtcNow.Date.AddDays(14).AddMonths(5),
        CompanyId = _company.Id,
        AcademicTrackId = _track.Id,
        CompanySupervisorName = "Tuteur",
        CompanySupervisorEmail = "t@test.com",
        InternshipOfferId = offerId
    };

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
            StartDate = DateTime.UtcNow.Date.AddDays(7),
            EndDate = DateTime.UtcNow.Date.AddDays(7).AddMonths(5),
            CompanyId = _company.Id,
            CompanySupervisorName = "Tuteur",
            CompanySupervisorEmail = "t@test.com"
        }, "std1", CancellationToken.None);
        update.IsSuccess.Should().BeTrue(update.IsFailure ? update.Error : string.Empty);

        var submit = await _service.SubmitAsync(internship.Id, "std1", CancellationToken.None);
        submit.IsSuccess.Should().BeTrue(submit.IsFailure ? submit.Error : string.Empty);

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
    public async Task AssignSupervisor_FromAnotherTrack_IsRejected()
    {
        var internship = AddProposedInternship();

        var result = await _service.AssignSupervisorAsync(internship.Id, "prof-gtr", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("autre filière");
    }

    [Fact]
    public async Task Propose_WhenStudentAlreadyHasAnEngagedInternship_IsRejected()
    {
        AddProposedInternship();

        var result = await _service.ProposeAsync(ValidProposal(), "std1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("déjà un dossier engagé");
    }

    [Fact]
    public async Task Submit_SecondDraft_WhileAnotherIsEngaged_IsRejected()
    {
        var draft = new Internship("Brouillon", "Desc", DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(10).AddMonths(4), "std1", _company.Id, _track.Id, "Tuteur", "t@test.com");
        _context.Internships.Add(draft);
        _context.SaveChanges();
        AddProposedInternship();

        var result = await _service.SubmitAsync(draft.Id, "std1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _context.Internships.Single(i => i.Id == draft.Id).Status.Should().Be(InternshipStatus.Brouillon);
    }

    [Fact]
    public async Task Approve_WhenStudentAlreadyHasAValidatedInternship_IsRejected()
    {
        var first = AddProposedInternship();
        (await _service.ApproveAsync(first.Id, "head", CancellationToken.None)).IsSuccess.Should().BeTrue();

        // Données héritées : un second dossier « Proposé » du même élève existe malgré la règle.
        var second = AddProposedInternship();

        var result = await _service.ApproveAsync(second.Id, "head", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("un seul stage");
    }

    [Theory]
    [InlineData(-3, 120, "passé")]
    [InlineData(7, 21, "minimum réglementaire")]
    [InlineData(7, 400, "maximum autorisé")]
    public async Task Propose_WithInvalidPeriod_IsRejected(int startInDays, int durationDays, string expectedError)
    {
        var start = DateTime.UtcNow.Date.AddDays(startInDays);
        var result = await _service.ProposeAsync(ValidProposal(start, start.AddDays(durationDays)), "std1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(expectedError);
    }

    [Fact]
    public async Task Propose_ForOffer_WithIncompatibleDuration_IsRejected_ButMatchingDurationIsAccepted()
    {
        var offer = new InternshipOffer("PFE .NET", "Desc", "C#", "Casablanca", 24, null, DateTime.UtcNow.AddMonths(2), _company.Id, _track.Id);
        _context.InternshipOffers.Add(offer);
        _context.SaveChanges();
        var start = DateTime.UtcNow.Date.AddDays(14);

        var tooShort = await _service.ProposeAsync(ValidProposal(start, start.AddDays(12 * 7), offer.Id), "std1", CancellationToken.None);
        tooShort.IsFailure.Should().BeTrue();
        tooShort.Error.Should().Contain("ne correspond pas à celle de l'offre");

        var matching = await _service.ProposeAsync(ValidProposal(start, start.AddDays(23 * 7), offer.Id), "std1", CancellationToken.None);
        matching.IsSuccess.Should().BeTrue(matching.IsFailure ? matching.Error : string.Empty);
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
    public async Task ConventionIsFullySigned_OnlyAfterAllThreeParties_CompanySigningViaEmailedLink()
    {
        var internship = AddProposedInternship();
        await _service.ApproveAsync(internship.Id, "head", CancellationToken.None);
        var conventions = CreateConventionService();

        (await conventions.SignAsync(internship.Id, ConventionParty.Student, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await conventions.SignAsync(internship.Id, ConventionParty.Student, CancellationToken.None)).IsFailure.Should().BeTrue("une partie ne signe qu'une fois");
        (await conventions.SignAsync(internship.Id, ConventionParty.Company, CancellationToken.None)).IsFailure
            .Should().BeTrue("l'entreprise ne peut pas être signée « pour son compte » sans lien ni scan");

        // Le lien est envoyé au tuteur entreprise ; on récupère le jeton dans le corps de l'e-mail.
        string? body = null;
        _emailSender.Setup(e => e.SendEmailAsync("t@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string html, CancellationToken _) => body = html)
            .Returns(Task.CompletedTask);
        (await conventions.SendCompanySigningLinkAsync(internship.Id, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var token = System.Text.RegularExpressions.Regex.Match(body!, "/convention/sign/([0-9a-f]+)").Groups[1].Value;

        var info = await conventions.GetCompanySigningInfoAsync(token, CancellationToken.None);
        info.IsSuccess.Should().BeTrue();
        info.Value!.CompanyName.Should().Be("Tech");

        (await conventions.SignByCompanyAsync(token, new CompanySignCommand("Mme Alaoui, DRH", true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await conventions.SignByCompanyAsync(token, new CompanySignCommand("Rejeu", true), CancellationToken.None)).IsFailure
            .Should().BeTrue("le lien est à usage unique");

        _context.Conventions.Single().IsFullySigned.Should().BeFalse();
        (await conventions.SignAsync(internship.Id, ConventionParty.School, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var stored = _context.Conventions.Single();
        stored.CompanySignerName.Should().Be("Mme Alaoui, DRH");
        var verification = await conventions.VerifyConventionAsync(stored.VerificationToken, CancellationToken.None);
        verification.Value!.IsFullySigned.Should().BeTrue();
    }

    [Fact]
    public async Task RecordCompanySignature_RequiresTheSignedScanFirst()
    {
        var internship = AddProposedInternship();
        await _service.ApproveAsync(internship.Id, "head", CancellationToken.None);
        var conventions = CreateConventionService();

        var withoutScan = await conventions.RecordCompanySignatureAsync(internship.Id, "M. Benali", CancellationToken.None);
        withoutScan.IsFailure.Should().BeTrue();
        withoutScan.Error.Should().Contain("scan");

        var documents = new DocumentService(_context, new InMemoryFileStorage());
        var upload = await documents.UploadAsync(internship.Id, InternshipDocumentKind.SignedConvention, "convention.pdf", 1024, new MemoryStream(FakePdf()), "head", CancellationToken.None);
        upload.IsSuccess.Should().BeTrue(upload.IsFailure ? upload.Error : string.Empty);

        (await conventions.RecordCompanySignatureAsync(internship.Id, "M. Benali", CancellationToken.None)).IsSuccess.Should().BeTrue();
        _context.Conventions.Single().IsSignedByCompany.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadPdf_WithoutConvention_ShouldFailWithoutCreatingOne()
    {
        var internship = AddProposedInternship();

        var result = await CreateConventionService().DownloadConventionPdfAsync(internship.Id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _context.Conventions.Should().BeEmpty();
    }
}
