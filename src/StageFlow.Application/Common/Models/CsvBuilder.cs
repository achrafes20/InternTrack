using System.Text;

namespace StageFlow.Application.Common.Models;

/// <summary>
/// Construit un CSV lisible par Excel en français : séparateur « ; », guillemets échappés, retour CRLF.
/// Les cellules commençant par = + - @ (ou une tabulation / un retour chariot) sont préfixées d'une apostrophe :
/// sans cela, un titre de stage saisi par un élève comme « =HYPERLINK(...) » serait exécuté comme formule
/// à l'ouverture du fichier (injection CSV / DDE).
/// </summary>
public sealed class CsvBuilder
{
    private const char Separator = ';';
    private static readonly char[] FormulaTriggers = { '=', '+', '-', '@', '\t', '\r' };

    private readonly StringBuilder _builder = new();

    public CsvBuilder AddRow(params string?[] cells)
    {
        _builder.Append(string.Join(Separator, cells.Select(Escape)));
        _builder.Append("\r\n");
        return this;
    }

    public static string Escape(string? value)
    {
        var cell = value ?? string.Empty;
        if (cell.Length > 0 && FormulaTriggers.Contains(cell[0]))
            cell = "'" + cell;

        var needsQuotes = cell.IndexOfAny(new[] { Separator, '"', '\n', '\r' }) >= 0 || cell != cell.Trim();
        return needsQuotes ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;
    }

    public override string ToString() => _builder.ToString();

    /// <summary>Encodage UTF-8 avec BOM : Excel détecte alors correctement les accents.</summary>
    public static byte[] ToExcelBytes(string csv) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
}
