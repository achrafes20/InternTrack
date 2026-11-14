namespace StageFlow.Application.Common.Interfaces;

public interface IPdfGenerator
{
    byte[] GenerateConventionPdf(
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
        byte[] qrCodeBytes);
}

