using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using InternTrack.Application.Common.Interfaces;

namespace InternTrack.Infrastructure.Services.Storage;

/// <summary>
/// Stockage sur disque (volume Docker en conteneur). Les fichiers sont enregistrés sous un nom aléatoire,
/// jamais sous le nom fourni par l'utilisateur : aucun parcours de répertoire ni écrasement possible.
/// Pour plusieurs instances, remplacer par un stockage partagé (Azure Blob…) implémentant IFileStorage.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Storage:RootPath"];
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "uploads")
            : configured);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var safeExtension = new string((extension ?? string.Empty).TrimStart('.').Where(char.IsLetterOrDigit).Take(10).ToArray());
        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{(safeExtension.Length > 0 ? safeExtension : "bin")}";

        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Clé de stockage invalide.");
        return fullPath;
    }
}
