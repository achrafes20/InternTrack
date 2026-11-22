using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Domain.Common;
using InternTrack.Domain.Entities;
using InternTrack.Infrastructure.Persistence;
using InternTrack.Infrastructure.Persistence.Outbox;
using InternTrack.Infrastructure.Services.Email;

namespace InternTrack.Application.UnitTests.Notifications;

public class EmailOutboxTests
{
    private sealed record TestEvent(DateTime OccurredOn) : IDomainEvent;

    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RetryDelay_IsExponential_AndCappedAtOneHour()
    {
        EmailOutboxMessage.RetryDelay(1).Should().Be(TimeSpan.FromSeconds(30));
        EmailOutboxMessage.RetryDelay(2).Should().Be(TimeSpan.FromMinutes(1));
        EmailOutboxMessage.RetryDelay(3).Should().Be(TimeSpan.FromMinutes(2));
        EmailOutboxMessage.RetryDelay(20).Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void MarkAttemptFailed_GivesUp_AfterMaxAttempts_AndCanBeRequeued()
    {
        var message = EmailOutboxMessage.Create("a@ensa.ma", "Objet", "<p>x</p>", Now);

        for (var i = 0; i < EmailOutboxMessage.MaxAttempts - 1; i++)
            message.MarkAttemptFailed("SMTP indisponible", Now);
        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.NextAttemptAt.Should().BeAfter(Now);

        message.MarkAttemptFailed("SMTP indisponible", Now);
        message.Status.Should().Be(EmailOutboxStatus.Failed);

        message.Requeue(Now);
        message.Status.Should().Be(EmailOutboxStatus.Pending);
        message.Attempts.Should().Be(0);
    }

    [Fact]
    public void MarkSent_ErasesBody_WhichMayContainPersonalLinks()
    {
        var message = EmailOutboxMessage.Create("a@ensa.ma", "Objet", "<a href='https://x/reset?token=secret'>lien</a>", Now);

        message.MarkSent(Now);

        message.Status.Should().Be(EmailOutboxStatus.Sent);
        message.HtmlBody.Should().BeEmpty();
        message.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task OutboxEmailSender_OutsideEventDispatch_PersistsMessageImmediately()
    {
        await using var context = TestHelpers.CreateInMemoryDbContext();
        var sender = new OutboxEmailSender(context, TimeProvider.System);

        await sender.SendEmailAsync("etudiant@ensa.ma", "Objet", "<p>Bonjour</p>", CancellationToken.None);

        context.ChangeTracker.Clear();
        (await context.EmailOutbox.SingleAsync()).ToEmail.Should().Be("etudiant@ensa.ma");
    }

    [Fact]
    public async Task DomainEventHandlerEmails_AreSavedWithTheBusinessChange()
    {
        var options = new DbContextOptionsBuilder<InternTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        InternTrackDbContext? context = null;
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyCollection<IDomainEvent> _, CancellationToken ct) =>
            {
                context!.IsDispatchingDomainEvents.Should().BeTrue();
                await new OutboxEmailSender(context, TimeProvider.System)
                    .SendEmailAsync("responsable@ensa.ma", "Nouveau dossier", "<p>…</p>", ct);
            });

        context = new InternTrackDbContext(options, null, null, dispatcher.Object);
        var track = new AcademicTrack("TST", "Filière test", "Description");
        track.AddDomainEvent(new TestEvent(Now));
        context.AcademicTracks.Add(track);

        await context.SaveChangesAsync();

        context.IsDispatchingDomainEvents.Should().BeFalse();
        await using var verify = new InternTrackDbContext(options);
        (await verify.AcademicTracks.CountAsync()).Should().Be(1);
        (await verify.EmailOutbox.SingleAsync()).ToEmail.Should().Be("responsable@ensa.ma");
    }

    [Fact]
    public async Task Processor_RetriesLater_WhenTransportFails_ThenSends()
    {
        var databaseName = Guid.NewGuid().ToString();
        var transport = new Mock<IEmailTransport>();
        transport
            .SetupSequence(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponible"))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddDbContext<InternTrackDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped(_ => transport.Object);
        await using var provider = services.BuildServiceProvider();

        var clock = new MutableTimeProvider(Now);
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InternTrackDbContext>();
            context.EmailOutbox.Add(EmailOutboxMessage.Create("a@ensa.ma", "Objet", "<p>x</p>", Now));
            await context.SaveChangesAsync();
        }

        var processor = new EmailOutboxProcessor(provider, new EmailOutboxSignal(), NullLogger<EmailOutboxProcessor>.Instance, clock);

        (await processor.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);
        await AssertMessage(provider, m =>
        {
            m.Status.Should().Be(EmailOutboxStatus.Pending);
            m.Attempts.Should().Be(1);
            m.LastError.Should().Contain("SMTP indisponible");
        });

        // Avant l'échéance du nouvel essai : rien n'est renvoyé.
        (await processor.ProcessBatchAsync(CancellationToken.None)).Should().Be(0);

        clock.Advance(TimeSpan.FromMinutes(1));
        (await processor.ProcessBatchAsync(CancellationToken.None)).Should().Be(1);
        await AssertMessage(provider, m =>
        {
            m.Status.Should().Be(EmailOutboxStatus.Sent);
            m.HtmlBody.Should().BeEmpty();
        });
    }

    private static async Task AssertMessage(IServiceProvider provider, Action<EmailOutboxMessage> assert)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InternTrackDbContext>();
        assert(await context.EmailOutbox.AsNoTracking().SingleAsync());
    }

    private sealed class MutableTimeProvider(DateTime utcNow) : TimeProvider
    {
        private DateTimeOffset _now = new(utcNow);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
