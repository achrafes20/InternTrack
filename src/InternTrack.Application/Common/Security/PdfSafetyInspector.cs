using System.IO.Compression;
using System.Text;

namespace InternTrack.Application.Common.Security;

public sealed record PdfInspectionResult(bool IsSafe, string? Reason)
{
    public static readonly PdfInspectionResult Safe = new(true, null);
    public static PdfInspectionResult Rejected(string reason) => new(false, reason);
}

/// <summary>
/// Analyse statique d'un PDF déposé : refuse le contenu actif (JavaScript, lancement de programme,
/// formulaires XFA, envoi de données) et les fichiers joints, qui sont les vecteurs d'attaque classiques
/// d'un PDF piégé. Un mémoire ou une convention scannée n'en a jamais besoin.
/// <para>
/// L'analyse porte sur les dictionnaires PDF, là où ces fonctions sont déclarées :
/// <list type="bullet">
/// <item>les objets en clair (le contenu des flux — texte des pages, images — est ignoré, ce qui évite
/// les faux positifs sur un mémoire qui parlerait de JavaScript) ;</item>
/// <item>les « object streams » compressés (PDF 1.5+), décompressés en mémoire avec une limite de taille ;</item>
/// <item>les noms obfusqués (<c>/J#61vaScript</c>) sont décodés avant comparaison.</item>
/// </list>
/// Un PDF chiffré ne peut pas être inspecté : il est refusé.
/// </para>
/// </summary>
public static class PdfSafetyInspector
{
    private const long MaxDecompressedBytesPerStream = 10 * 1024 * 1024;
    private const long MaxDecompressedBytesTotal = 50 * 1024 * 1024;

    private static readonly Dictionary<string, string> ForbiddenNames = new(StringComparer.Ordinal)
    {
        ["JavaScript"] = "du code JavaScript",
        ["JS"] = "du code JavaScript",
        ["Launch"] = "une action de lancement de programme",
        ["EmbeddedFile"] = "des fichiers joints",
        ["EmbeddedFiles"] = "des fichiers joints",
        ["FileAttachment"] = "des fichiers joints",
        ["RichMedia"] = "du contenu multimédia actif",
        ["XFA"] = "un formulaire XFA",
        ["SubmitForm"] = "un envoi de formulaire",
        ["ImportData"] = "une importation de données",
        ["GoToE"] = "un lien vers un document joint"
    };

    private static readonly HashSet<string> NonFlateFilters = new(StringComparer.Ordinal)
    {
        "LZWDecode", "ASCIIHexDecode", "ASCII85Decode", "RunLengthDecode", "Crypt"
    };

    public static PdfInspectionResult Inspect(ReadOnlySpan<byte> pdf)
    {
        var budget = MaxDecompressedBytesTotal;
        return Scan(pdf, allowNestedStreams: true, ref budget);
    }

    private static PdfInspectionResult Scan(ReadOnlySpan<byte> data, bool allowNestedStreams, ref long budget)
    {
        // Noms rencontrés depuis le dernier flux : ils décrivent le dictionnaire du prochain flux.
        var dictionaryNames = new List<string>();
        var i = 0;

        while (i < data.Length)
        {
            var c = data[i];

            if (c == (byte)'%')
            {
                while (i < data.Length && data[i] != (byte)'\n' && data[i] != (byte)'\r') i++;
                continue;
            }

            if (c == (byte)'(')
            {
                i = SkipLiteralString(data, i);
                continue;
            }

            if (c == (byte)'/')
            {
                var name = ReadName(data, ref i);
                if (ForbiddenNames.TryGetValue(name, out var description))
                    return PdfInspectionResult.Rejected($"Le PDF contient {description} : ce type de contenu est refusé pour des raisons de sécurité.");
                if (name == "Encrypt")
                    return PdfInspectionResult.Rejected("Les PDF chiffrés ou protégés par mot de passe ne sont pas acceptés.");
                dictionaryNames.Add(name);
                continue;
            }

            if (c == (byte)'s' && IsStreamKeyword(data, i))
            {
                var dataStart = SkipEndOfLine(data, i + 6);
                var end = data[dataStart..].IndexOf("endstream"u8);
                if (end < 0)
                    return PdfInspectionResult.Rejected("Le PDF est incomplet ou corrompu.");

                var streamData = data.Slice(dataStart, end);
                if (dictionaryNames.Contains("ObjStm"))
                {
                    if (!allowNestedStreams)
                        return PdfInspectionResult.Rejected("La structure du PDF n'a pas pu être analysée.");

                    var inner = InspectObjectStream(streamData, dictionaryNames, ref budget);
                    if (!inner.IsSafe) return inner;
                }

                dictionaryNames.Clear();
                i = dataStart + end + "endstream".Length;
                continue;
            }

            if (c == (byte)'e' && data[i..].StartsWith("endobj"u8))
                dictionaryNames.Clear();

            i++;
        }

        return PdfInspectionResult.Safe;
    }

    private static PdfInspectionResult InspectObjectStream(ReadOnlySpan<byte> streamData, List<string> dictionaryNames, ref long budget)
    {
        if (dictionaryNames.Any(NonFlateFilters.Contains))
            return PdfInspectionResult.Rejected("La structure du PDF utilise un encodage non pris en charge et n'a pas pu être vérifiée.");

        if (!dictionaryNames.Contains("FlateDecode"))
            return Scan(streamData, allowNestedStreams: false, ref budget);

        byte[] decompressed;
        try
        {
            decompressed = Inflate(streamData, Math.Min(MaxDecompressedBytesPerStream, budget));
        }
        catch (InvalidDataException)
        {
            return PdfInspectionResult.Rejected("Le PDF est corrompu : une partie de sa structure est illisible.");
        }
        catch (InvalidOperationException)
        {
            return PdfInspectionResult.Rejected("La structure du PDF est anormalement volumineuse ; le fichier a été refusé par précaution.");
        }

        budget -= decompressed.Length;
        return Scan(decompressed, allowNestedStreams: false, ref budget);
    }

    private static byte[] Inflate(ReadOnlySpan<byte> compressed, long limit)
    {
        using var input = new MemoryStream(compressed.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = zlib.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > limit)
                throw new InvalidOperationException("Limite de décompression atteinte.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static bool IsDelimiterOrWhitespace(byte b) => b switch
    {
        (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or 0 => true,
        (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']'
            or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%' => true,
        _ => false
    };

    /// <summary>Lit un nom PDF à partir du « / » et décode les séquences #xx.</summary>
    private static string ReadName(ReadOnlySpan<byte> data, ref int i)
    {
        i++; // « / »
        var builder = new StringBuilder();
        while (i < data.Length && !IsDelimiterOrWhitespace(data[i]))
        {
            if (data[i] == (byte)'#' && i + 2 < data.Length && IsHex(data[i + 1]) && IsHex(data[i + 2]))
            {
                builder.Append((char)Convert.ToByte(Encoding.ASCII.GetString(data.Slice(i + 1, 2)), 16));
                i += 3;
                continue;
            }
            builder.Append((char)data[i]);
            i++;
            if (builder.Length > 127) break;
        }
        return builder.ToString();
    }

    private static bool IsHex(byte b) => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F';

    private static int SkipLiteralString(ReadOnlySpan<byte> data, int i)
    {
        var depth = 0;
        for (; i < data.Length; i++)
        {
            var b = data[i];
            if (b == (byte)'\\') { i++; continue; }
            if (b == (byte)'(') depth++;
            else if (b == (byte)')' && --depth == 0) return i + 1;
        }
        return data.Length;
    }

    private static bool IsStreamKeyword(ReadOnlySpan<byte> data, int i)
    {
        if (!data[i..].StartsWith("stream"u8)) return false;
        if (i > 0 && !(IsDelimiterOrWhitespace(data[i - 1]))) return false;
        var after = i + 6;
        return after < data.Length && (data[after] == (byte)'\r' || data[after] == (byte)'\n');
    }

    private static int SkipEndOfLine(ReadOnlySpan<byte> data, int i)
    {
        if (i < data.Length && data[i] == (byte)'\r') i++;
        if (i < data.Length && data[i] == (byte)'\n') i++;
        return i;
    }
}
