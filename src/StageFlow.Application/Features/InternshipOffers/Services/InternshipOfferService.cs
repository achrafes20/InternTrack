using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Models;
using StageFlow.Application.Features.InternshipOffers.DTOs;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;

namespace StageFlow.Application.Features.InternshipOffers.Services;

public interface IInternshipOfferService
{
    Task<PaginatedList<InternshipOfferDto>> GetOffersAsync(
        string? searchTerm,
        string? companyName,
        string? city,
        string? skill,
        Guid? academicTrackId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<InternshipOfferDto>> GetOfferByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<InternshipOfferDto>> CreateOfferAsync(CreateInternshipOfferCommand command, CancellationToken cancellationToken);
}

public class InternshipOfferService : IInternshipOfferService
{
    private readonly IApplicationDbContext _context;

    public InternshipOfferService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<InternshipOfferDto>> GetOffersAsync(
        string? searchTerm,
        string? companyName,
        string? city,
        string? skill,
        Guid? academicTrackId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.InternshipOffers
            .Include(o => o.Company)
            .Include(o => o.AcademicTrack)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(o => o.Title.ToLower().Contains(term) || o.Description.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            query = query.Where(o => o.Company.Name.ToLower().Contains(companyName.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(o => o.City.ToLower() == city.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(skill))
        {
            query = query.Where(o => o.RequiredSkills.ToLower().Contains(skill.ToLower()));
        }

        if (academicTrackId.HasValue)
        {
            query = query.Where(o => o.AcademicTrackId == academicTrackId.Value);
        }

        var dtoQuery = query.OrderByDescending(o => o.CreatedAt)
            .Select(o => new InternshipOfferDto(
                o.Id,
                o.Title,
                o.Description,
                o.RequiredSkills,
                o.City,
                o.DurationInWeeks,
                o.MonthlyStipend,
                o.IsActive,
                o.ExpirationDate,
                o.CompanyId,
                o.Company.Name,
                o.AcademicTrackId,
                o.AcademicTrack.Code));

        return await PaginatedList<InternshipOfferDto>.CreateAsync(dtoQuery, pageNumber, pageSize, cancellationToken);
    }

    public async Task<Result<InternshipOfferDto>> GetOfferByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _context.InternshipOffers
            .Include(o => o.Company)
            .Include(o => o.AcademicTrack)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer == null)
            return Result<InternshipOfferDto>.Failure("Offre de stage introuvable.");

        var dto = new InternshipOfferDto(
            offer.Id,
            offer.Title,
            offer.Description,
            offer.RequiredSkills,
            offer.City,
            offer.DurationInWeeks,
            offer.MonthlyStipend,
            offer.IsActive,
            offer.ExpirationDate,
            offer.CompanyId,
            offer.Company.Name,
            offer.AcademicTrackId,
            offer.AcademicTrack.Code);

        return Result<InternshipOfferDto>.Success(dto);
    }

    public async Task<Result<InternshipOfferDto>> CreateOfferAsync(CreateInternshipOfferCommand command, CancellationToken cancellationToken)
    {
        var company = await _context.Companies.FindAsync(new object[] { command.CompanyId }, cancellationToken);
        if (company == null)
            return Result<InternshipOfferDto>.Failure("L'entreprise spécifiée n'existe pas.");

        var track = await _context.AcademicTracks.FindAsync(new object[] { command.AcademicTrackId }, cancellationToken);
        if (track == null)
            return Result<InternshipOfferDto>.Failure("La filière spécifiée n'existe pas.");

        var offer = new InternshipOffer(
            command.Title,
            command.Description,
            command.RequiredSkills,
            command.City,
            command.DurationInWeeks,
            command.MonthlyStipend,
            command.ExpirationDate,
            command.CompanyId,
            command.AcademicTrackId);

        await _context.InternshipOffers.AddAsync(offer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new InternshipOfferDto(
            offer.Id,
            offer.Title,
            offer.Description,
            offer.RequiredSkills,
            offer.City,
            offer.DurationInWeeks,
            offer.MonthlyStipend,
            offer.IsActive,
            offer.ExpirationDate,
            company.Id,
            company.Name,
            track.Id,
            track.Code);

        return Result<InternshipOfferDto>.Success(dto);
    }
}

