using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Infrastructure.Services.Pdf;

public class QuestPdfGenerator : IPdfGenerator
{
    public QuestPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateConventionPdf(
        string conventionNumber,
        string studentFullName,
        string studentEmail,
        string studentNumber,
        string trackName,
        string companyName,
        string companyAddress,
        string supervisorName,
        string internshipTitle,
        string subjectDescription,
        DateTime startDate,
        DateTime endDate,
        byte[] qrCodeBytes)
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
                        col.Item().Text($"Filière : {trackName}").FontSize(10).Italic();
                    });

                    row.ConstantItem(75).Height(75).Image(qrCodeBytes);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().AlignCenter().Text("CONVENTION DE STAGE OFFICIELLE").Bold().FontSize(15).Underline();
                    col.Item().AlignCenter().Text($"Réf : {conventionNumber}").FontSize(10).FontColor(Colors.Grey.Darken2);

                    col.Item().PaddingTop(15).Text("ENTRE LES SOUSSIGNÉS :").Bold().FontSize(11);
                    col.Item().Text($"1. L'Établissement d'Enseignement : École Nationale des Sciences Appliquées (ENSA)");
                    col.Item().Text($"2. L'Entreprise d'Accueil : {companyName}, située à {companyAddress}");
                    col.Item().Text($"3. L'Élève Ingénieur : {studentFullName} (Code Apogée : {studentNumber}, Email : {studentEmail})");

                    col.Item().PaddingTop(15).Text("ARTICLE 1 : OBJET DU STAGE").Bold();
                    col.Item().Text($"Intitulé : {internshipTitle}").Bold();
                    col.Item().Text($"Description du sujet : {subjectDescription}");

                    col.Item().PaddingTop(10).Text("ARTICLE 2 : DURÉE ET PÉRIODE").Bold();
                    col.Item().Text($"Le stage se déroulera du {startDate:dd/MM/yyyy} au {endDate:dd/MM/yyyy}.");

                    col.Item().PaddingTop(10).Text("ARTICLE 3 : ENCADREMENT").Bold();
                    col.Item().Text($"Tuteur en entreprise désigné : {supervisorName}.");

                    col.Item().PaddingTop(10).Text("ARTICLE 4 : AUTHENTICITÉ ET VÉRIFICATION").Bold();
                    col.Item().Text("Ce document est certifié par signature électronique. Scannez le QR Code en en-tête pour vérifier l'authenticité et le statut de validité de cette convention sur le portail public StageFlow.");

                    col.Item().PaddingTop(25).Row(signRow =>
                    {
                        signRow.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Pour l'Élève Ingénieur").Bold();
                            c.Item().Text("Signature et mention 'Lu et approuvé'");
                        });

                        signRow.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Pour l'Entreprise").Bold();
                            c.Item().Text("Cachet et signature du tuteur");
                        });

                        signRow.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Pour l'École (ENSA)").Bold();
                            c.Item().Text("Le Directeur / Responsable de filière");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("StageFlow ENSA - Document généré électroniquement le ");
                    x.Span($"{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC").Bold();
                });
            });
        }).GeneratePdf();
    }
}

