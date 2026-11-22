using System.IO.Compression;
using System.Text;
using FluentAssertions;
using InternTrack.Application.Common.Security;

namespace InternTrack.Application.UnitTests.Security;

public class PdfSafetyInspectorTests
{
    private static byte[] Pdf(string body) => Encoding.Latin1.GetBytes($"%PDF-1.7\n{body}\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

    private const string Catalog = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";

    [Fact]
    public void PlainDocument_IsAccepted()
        => PdfSafetyInspector.Inspect(Pdf(Catalog)).IsSafe.Should().BeTrue();

    [Theory]
    [InlineData("<< /Type /Action /S /JavaScript /JS (app.alert(1)) >>")]
    [InlineData("<< /S /Launch /F (cmd.exe) >>")]
    [InlineData("<< /Names << /EmbeddedFiles 5 0 R >> >>")]
    [InlineData("<< /AcroForm << /XFA 7 0 R >> >>")]
    [InlineData("<< /S /SubmitForm /F (https://attaquant.example) >>")]
    public void ActiveContent_IsRejected(string dictionary)
    {
        var result = PdfSafetyInspector.Inspect(Pdf($"{Catalog}3 0 obj\n{dictionary}\nendobj\n"));

        result.IsSafe.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ObfuscatedName_IsDecoded_AndRejected()
        => PdfSafetyInspector.Inspect(Pdf($"{Catalog}3 0 obj\n<< /S /J#61vaScript /JS (x) >>\nendobj\n")).IsSafe.Should().BeFalse();

    [Fact]
    public void JavaScriptHiddenInCompressedObjectStream_IsRejected()
    {
        var hidden = Encoding.Latin1.GetBytes("4 0 << /S /JavaScript /JS (app.alert(1)) >>");
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(hidden);

        var header = Encoding.Latin1.GetBytes($"%PDF-1.7\n{Catalog}5 0 obj\n<< /Type /ObjStm /N 1 /First 4 /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
        var footer = Encoding.Latin1.GetBytes("\nendstream\nendobj\n%%EOF\n");
        var pdf = header.Concat(compressed.ToArray()).Concat(footer).ToArray();

        PdfSafetyInspector.Inspect(pdf).IsSafe.Should().BeFalse();
    }

    [Fact]
    public void TextAboutJavaScriptInsidePageContent_IsNotAFalsePositive()
    {
        // Le contenu des pages (flux non « ObjStm ») n'est pas analysé : un mémoire peut parler de /JavaScript.
        var body = $"{Catalog}6 0 obj\n<< /Length 40 >>\nstream\nBT (/JavaScript et /JS) Tj ET /JS\nendstream\nendobj\n";
        PdfSafetyInspector.Inspect(Pdf(body)).IsSafe.Should().BeTrue();
    }

    [Fact]
    public void StringContainingSlashName_IsNotAFalsePositive()
        => PdfSafetyInspector.Inspect(Pdf($"{Catalog}7 0 obj\n<< /Title (Etude de /JavaScript) >>\nendobj\n")).IsSafe.Should().BeTrue();

    [Fact]
    public void EncryptedPdf_IsRejected()
        => PdfSafetyInspector.Inspect(Pdf($"{Catalog}trailer\n<< /Encrypt 9 0 R >>\n")).IsSafe.Should().BeFalse();

    [Fact]
    public void CorruptedObjectStream_IsRejected()
    {
        var body = $"{Catalog}5 0 obj\n<< /Type /ObjStm /Filter /FlateDecode >>\nstream\nceci n'est pas du zlib\nendstream\nendobj\n";
        PdfSafetyInspector.Inspect(Pdf(body)).IsSafe.Should().BeFalse();
    }
}
