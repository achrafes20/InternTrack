using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Hosting;

namespace InternTrack.Infrastructure.Services.Storage;

/// <summary>
/// Crée le conteneur Blob (privé) au démarrage, avant la première requête : il héberge les fichiers déposés
/// et le trousseau de clés DataProtection partagé entre les instances.
/// </summary>
public class AzureBlobContainerInitializer : IHostedService
{
    private readonly BlobContainerClient _container;

    public AzureBlobContainerInitializer(BlobContainerClient container)
    {
        _container = container;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
