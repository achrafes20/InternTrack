namespace StageFlow.Application.Features.InternshipOffers.DTOs;

public record InternshipOfferDto(
    Guid Id,
    string Title,
    string Description,
    string RequiredSkills,
    string City,
    int DurationInWeeks,
    decimal? MonthlyStipend,
    bool IsActive,
    DateTime ExpirationDate,
    Guid CompanyId,
    string CompanyName,
    Guid AcademicTrackId,
    string AcademicTrackCode);

public record CreateInternshipOfferCommand(
    string Title,
    string Description,
    string RequiredSkills,
    string City,
    int DurationInWeeks,
    decimal? MonthlyStipend,
    DateTime ExpirationDate,
    Guid CompanyId,
    Guid AcademicTrackId);

