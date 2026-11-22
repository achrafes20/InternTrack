using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using InternTrack.Application.Common.Interfaces;

namespace InternTrack.Infrastructure.Services.Storage;

/// <summary>
/// Stockage des fichiers dans Azure Blob Storage : partagé par toutes les instances de l'API et indépendant
/// du cycle de vie des conteneurs. Les clés ont le même format que <see cref="LocalFileStorage"/>
/// (aaaa/mm/guid.ext) : une migration du disque vers Azure se fait par simple copie des fichiers.
/// <para>Configuration (section Storage:AzureBlob) :</para>
/// <list type="bullet">
/// <item>ConnectionString : chaîne de connexion (développement, Azurite) ;</item>
/// <item>ou AccountUrl : URL du compte, authentifiée par identité managée (recommandé en production) ;</item>
/// <item>ContainerName : nom du conteneur privé (défaut « interntrack-documents »).</item>
/// </list>
/// </summary>
public class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    public AzureBlobFileStorage(IConfiguration configuration)
        : this(CreateContainerClient(configuration))
    {
    }

    public AzureBlobFileStorage(BlobContainerClient container)
    {
        _container = container;
    }

    public static BlobContainerClient CreateContainerClient(IConfiguration configuration)
    {
        var section = configuration.GetSection("Storage:AzureBlob");
        var containerName = section["ContainerName"] ?? "interntrack-documents";
        var connectionString = section["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
            return new BlobContainerClient(connectionString, containerName);

        var accountUrl = section["AccountUrl"];
        if (string.IsNullOrWhiteSpace(accountUrl))
            throw new InvalidOperationException(
                "Storage:Provider=AzureBlob exige Storage:AzureBlob:ConnectionString ou Storage:AzureBlob:AccountUrl.");

        return new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential()).GetBlobContainerClient(containerName);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);

        var safeExtension = new string((extension ?? string.Empty).TrimStart('.').Where(char.IsLetterOrDigit).Take(10).ToArray());
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{(safeExtension.Length > 0 ? safeExtension : "bin")}";

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = ContentTypeFor(safeExtension) },
            // Échoue si la clé existe déjà : un fichier n'est jamais écrasé.
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        };
        await _container.GetBlobClient(ValidateKey(key)).UploadAsync(content, options, cancellationToken);
        return key;
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            var blob = _container.GetBlobClient(ValidateKey(storageKey));
            return await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _container.GetBlobClient(ValidateKey(storageKey))
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Conteneur absent : rien à supprimer.
        }
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            // Conteneur privé : aucun accès anonyme, les téléchargements passent par l'API (contrôle d'accès).
            await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Refuse toute clé qui ne correspond pas au format produit par SaveAsync (pas de « .. », pas d'URL).</summary>
    private static string ValidateKey(string storageKey)
    {
        var key = (storageKey ?? string.Empty).Replace('\\', '/');
        var parts = key.Split('/');
        if (parts.Length != 3 || parts.Any(p => p.Length == 0 || p == "." || p == "..")
            || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '-' or '_'))
            throw new InvalidOperationException("Clé de stockage invalide.");
        return key;
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        "pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
