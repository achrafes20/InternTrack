using System.Text;
using Azure.Storage.Blobs;
using FluentAssertions;
using InternTrack.Infrastructure.Services.Storage;
using Testcontainers.Azurite;
using Xunit;

namespace InternTrack.Api.IntegrationTests.Storage;

/// <summary>
/// Stockage Azure Blob contre l'émulateur officiel Azurite (Docker requis).
/// Pour exclure ces tests : dotnet test --filter "Category!=Azurite".
/// </summary>
[Trait("Category", "Azurite")]
public sealed class AzureBlobFileStorageTests : IAsyncLifetime
{
    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public Task InitializeAsync() => _azurite.StartAsync();

    public Task DisposeAsync() => _azurite.DisposeAsync().AsTask();

    private AzureBlobFileStorage CreateStorage()
        => new(new BlobContainerClient(_azurite.GetConnectionString(), $"test-{Guid.NewGuid():N}"));

    [Fact]
    public async Task SaveOpenDelete_RoundTrip()
    {
        var storage = CreateStorage();
        var content = Encoding.UTF8.GetBytes("%PDF-1.7 contenu de test");

        var key = await storage.SaveAsync(new MemoryStream(content), "pdf", CancellationToken.None);
        key.Should().MatchRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}\.pdf$");

        await using (var stream = await storage.OpenReadAsync(key, CancellationToken.None))
        {
            stream.Should().NotBeNull();
            using var copy = new MemoryStream();
            await stream!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(content);
        }

        await storage.DeleteAsync(key, CancellationToken.None);
        (await storage.OpenReadAsync(key, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task OpenRead_UnknownKey_ReturnsNull()
        => (await CreateStorage().OpenReadAsync("2026/11/00000000000000000000000000000000.pdf", CancellationToken.None))
            .Should().BeNull();

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("2026/../../x.pdf")]
    [InlineData("https://evil.example/x.pdf")]
    public async Task InvalidKeys_AreRejected(string key)
    {
        var act = () => CreateStorage().OpenReadAsync(key, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
