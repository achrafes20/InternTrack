using System.Security.Cryptography;
using System.Text;

namespace InternTrack.Domain.Common;

/// <summary>
/// Jetons des liens envoyés par e-mail à des personnes sans compte (signature entreprise, évaluation du tuteur).
/// Seul le hash SHA-256 est conservé en base ; la comparaison se fait en temps constant.
/// </summary>
public static class SecureToken
{
    /// <summary>Jeton aléatoire de 256 bits, encodé en hexadécimal (64 caractères).</summary>
    public static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    public static bool Matches(string? storedHash, string rawToken)
        => storedHash != null
           && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(storedHash), Encoding.ASCII.GetBytes(Hash(rawToken)));

    /// <summary>Empreinte SHA-256 d'un document (hexadécimal minuscule).</summary>
    public static string Fingerprint(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
