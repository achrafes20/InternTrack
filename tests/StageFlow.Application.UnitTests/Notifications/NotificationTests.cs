using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Models;
using StageFlow.Application.Features.Notifications;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Domain.Events;
using StageFlow.Infrastructure.Persistence;
using StageFlow.Infrastructure.Services.Background;
using Xunit;
using static StageFlow.Application.UnitTests.TestHelpers;

namespace StageFlow.Application.UnitTests.Notifications;

public class NotificationTests
{
    [Fact]
    public async Task ReminderWindows_AreContiguous_SoEveryDefenseIsNotifiedExactlyOnce()
    {
        var dbName = Guid.NewGuid().ToString();
        var emails = new List<string>();

        var services = new ServiceCollection();
        services.AddDbContext<StageFlowDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<StageFlowDbContext>());
        var emailSender = new Mock<IEmailSender>();
        emailSender.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string to, string _, string _, CancellationToken _) => emails.Add(to))
            .Returns(Task.CompletedTask);
        services.AddSingleton(emailSender.Object);
        services.AddSingleton(MoroccoTime);
        services.AddSingleton(CreateIdentityMock(Student("std1", "Etudiant Un", Guid.Empty), Teacher("prof1", "Professeur Un")).Object);
        var provider = services.BuildServiceProvider();

        var t0 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StageFlowDbContext>();
            var internship = new Internship("Stage", "Desc", t0, t0.AddMonths(4), "std1", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@test.com");
            context.Internships.Add(internship);
            // Soutenance 48 h + 37 min après t0 : tombe entre deux passages du service.
            var defense = new Defense(internship.Id, t0.AddHours(48).AddMinutes(37), "Salle A");
            context.Defenses.Add(defense);
            context.DefenseJuryMembers.Add(new DefenseJuryMember(defense.Id, "prof1", "Professeur Un", DefenseRole.President));
            await context.SaveChangesAsync();
        }

        var service = new NotificationBackgroundService(provider, NullLogger<NotificationBackgroundService>.Instance);
        var lead = NotificationBackgroundService.ReminderLeadTime;

        // Passages successifs toutes les 15 min pendant 2 h : chaque fenêtre commence où la précédente finit.
        var windowStart = t0;
        for (var tick = t0.AddMinutes(15); tick <= t0.AddHours(2); tick = tick.AddMinutes(15))
        {
            await service.ProcessWindowAsync(windowStart + lead, tick + lead, CancellationToken.None);
            windowStart = tick;
        }

        emails.Should().BeEquivalentTo(new[] { "std1@ensa.ma", "prof1@ensa.ma" });
    }

    [Fact]
    public async Task StatusChange_ToRefused_ShouldEmailTheStudentWithTheReason()
    {
        using var context = CreateInMemoryDbContext();
        var internship = new Internship("Mon PFE", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(4), "std1", Guid.NewGuid(), Guid.NewGuid(), "Tuteur", "t@test.com");
        internship.SubmitForApproval("std1");
        internship.Reject("head", "Sujet <hors> périmètre");
        context.Internships.Add(internship);
        await context.SaveChangesAsync();

        var emailSender = new Mock<IEmailSender>();
        var handler = new InternshipStatusChangedNotificationHandler(
            context,
            CreateIdentityMock(Student("std1", "Etudiant Un", Guid.Empty)).Object,
            emailSender.Object);

        await handler.HandleAsync(
            new InternshipStatusChangedEvent(internship.Id, InternshipStatus.Propose, InternshipStatus.Refuse, "head", DateTime.UtcNow),
            CancellationToken.None);

        emailSender.Verify(e => e.SendEmailAsync(
            "std1@ensa.ma",
            It.Is<string>(s => s.Contains("Mon PFE")),
            It.Is<string>(b => b.Contains("refusé") && b.Contains("Sujet &lt;hors&gt;")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 10, 1, 10)]
    [InlineData(-5, 0, 1, 1)]
    [InlineData(3, 1_000_000, 3, PaginatedList<int>.MaxPageSize)]
    public void Pagination_ShouldBeBounded(int page, int size, int expectedPage, int expectedSize)
    {
        PaginatedList<int>.Normalize(page, size).Should().Be((expectedPage, expectedSize));
    }
}
