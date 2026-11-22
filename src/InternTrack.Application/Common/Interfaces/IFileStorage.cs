namespace InternTrack.Application.Common.Interfaces;

/// <summary>Stockage des fichiers déposés (mémoires, conventions signées). Les clés sont opaques.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
