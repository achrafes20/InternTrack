using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InternTrack.Application.Common.Interfaces;

namespace InternTrack.Infrastructure.Services.Pdf;

public class QuestPdfGenerator : IPdfGenerator
{
    public QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateConventionPdf(ConventionPdfModel m)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("ÉCOLE NATIONALE DES SCIENCES APPLIQUÉES").Bold().FontSize(13).FontColor(Colors.Blue.Darken2);
                        col.Item().Text("Direction des Études & Stages PFE").FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"Filière : {m.TrackName}").FontSize(10).Italic();
                    });

                    row.ConstantItem(75).Height(75).Image(m.QrCodeBytes);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().AlignCenter().Text("CONVENTION DE STAGE").Bold().FontSize(15).Underline();
                    col.Item().AlignCenter().Text($"Réf : {m.ConventionNumber}").FontSize(10).FontColor(Colors.Grey.Darken2);

                    if (!m.SignedAt.HasValue)
                    {
                        col.Item().PaddingTop(8).Background(Colors.Orange.Lighten4).Padding(6).AlignCenter()
                            .Text("DOCUMENT EN ATTENTE DE SIGNATURE — sans valeur tant que les trois parties n'ont pas signé.")
                            .Bold().FontSize(9).FontColor(Colors.Orange.Darken4);
                    }

                    col.Item().PaddingTop(15).Text("ENTRE LES SOUSSIGNÉS :").Bold().FontSize(11);
                    col.Item().Text("1. L'Établissement d'Enseignement : École Nationale des Sciences Appliquées (ENSA)");
                    col.Item().Text($"2. L'Entreprise d'Accueil : {m.CompanyName}, située à {m.CompanyAddress}");
                    col.Item().Text($"3. L'Élève Ingénieur : {m.StudentFullName} (Code Apogée : {m.StudentNumber}, Email : {m.StudentEmail})");

                    col.Item().PaddingTop(15).Text("ARTICLE 1 : OBJET DU STAGE").Bold();
                    col.Item().Text($"Intitulé : {m.InternshipTitle}").Bold();
                    col.Item().Text($"Description du sujet : {m.SubjectDescription}");

                    col.Item().PaddingTop(10).Text("ARTICLE 2 : DURÉE ET PÉRIODE").Bold();
                    col.Item().Text($"Le stage se déroulera du {m.StartDate:dd/MM/yyyy} au {m.EndDate:dd/MM/yyyy}.");

                    col.Item().PaddingTop(10).Text("ARTICLE 3 : ENCADREMENT").Bold();
                    col.Item().Text($"Tuteur en entreprise désigné : {m.SupervisorName}.");

                    col.Item().PaddingTop(10).Text("ARTICLE 4 : AUTHENTICITÉ ET VÉRIFICATION").Bold();
                    col.Item().Text("Le QR code en en-tête renvoie vers le portail public InternTrack, qui affiche les informations " +
                                    "enregistrées pour cette convention et l'état de chaque signature. En cas de divergence entre " +
                                    "ce document et le portail, seules les informations du portail font foi.");
                    col.Item().Text(m.VerificationUrl).FontSize(8).FontColor(Colors.Blue.Darken1);

                    col.Item().PaddingTop(25).Row(signRow =>
                    {
                        SignatureBlock(signRow, "Pour l'Élève Ingénieur", "Signature et mention 'Lu et approuvé'", m.IsSignedByStudent);
                        SignatureBlock(signRow, "Pour l'Entreprise",
                            m.CompanySignerName is { Length: > 0 } ? $"Signé par {m.CompanySignerName}" : "Cachet et signature du tuteur",
                            m.IsSignedByCompany);
                        SignatureBlock(signRow, "Pour l'École (ENSA)", "Le Directeur / Responsable de filière", m.IsSignedBySchool);
                    });

                    if (m.SignedAt.HasValue)
                    {
                        col.Item().PaddingTop(10).AlignCenter()
                            .Text($"Convention signée par les trois parties le {m.SignedAtLabel ?? m.SignedAt.Value.ToString("dd/MM/yyyy HH:mm") + " UTC"}.")
                            .FontSize(9).FontColor(Colors.Green.Darken3);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("InternTrack ENSA - Document généré le ");
                    x.Span(m.GeneratedAtLabel ?? $"{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC").Bold();
                });
            });
        }).GeneratePdf();
    }

    private static void SignatureBlock(RowDescriptor row, string title, string caption, bool signed)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(title).Bold();
            c.Item().Text(caption);
            c.Item().PaddingTop(4).Text(signed ? "✔ Signature enregistrée" : "En attente de signature")
                .FontSize(9)
                .FontColor(signed ? Colors.Green.Darken3 : Colors.Orange.Darken3);
        });
    }
}
