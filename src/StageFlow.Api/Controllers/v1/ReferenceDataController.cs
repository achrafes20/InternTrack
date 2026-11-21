using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Entities;

namespace StageFlow.Api.Controllers.v1;

public record CreateCompanyRequest(
    string Name,
    string? Industry,
    string City,
    string? Address,
    string ContactEmail,
    string? ContactPhone,
    string? Website);

[Authorize]
public class ReferenceDataController : ApiControllerBase
{
    private readonly IApplicationDbContext _context;

    public ReferenceDataController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("/api/v1/companies")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
    {
        var companies = await _context.Companies
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Industry,
                c.City,
                c.Address,
                c.ContactEmail,
                c.ContactPhone,
                c.Website
            })
            .ToListAsync(cancellationToken);

        return Ok(companies);
    }

    [HttpPost("/api/v1/companies")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.ContactEmail))
            return BadRequest(new ProblemDetails { Title = "Entreprise invalide", Detail = "Le nom, la ville et l'email de contact sont obligatoires.", Status = 400 });

        var name = request.Name.Trim();
        if (await _context.Companies.AnyAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken))
            return BadRequest(new ProblemDetails { Title = "Entreprise invalide", Detail = "Une entreprise portant ce nom existe déjà.", Status = 400 });

        var company = new Company(
            name,
            request.Industry ?? string.Empty,
            request.City.Trim(),
            request.Address ?? string.Empty,
            request.ContactEmail.Trim(),
            request.ContactPhone,
            request.Website);

        await _context.Companies.AddAsync(company, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Created($"/api/v1/companies/{company.Id}", new
        {
            company.Id,
            company.Name,
            company.Industry,
            company.City,
            company.Address,
            company.ContactEmail,
            company.ContactPhone,
            company.Website
        });
    }

    [HttpPut("/api/v1/companies/{id:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> UpdateCompany(Guid id, [FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (company == null)
            return NotFound(new ProblemDetails { Title = "Entreprise introuvable", Status = 404 });

        var name = request.Name.Trim();
        if (await _context.Companies.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), cancellationToken))
            return BadRequest(new ProblemDetails { Title = "Entreprise invalide", Detail = "Une autre entreprise porte déjà ce nom.", Status = 400 });

        company.Update(
            name,
            request.Industry ?? string.Empty,
            request.City.Trim(),
            request.Address ?? string.Empty,
            request.ContactEmail.Trim(),
            request.ContactPhone,
            request.Website);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            company.Id,
            company.Name,
            company.Industry,
            company.City,
            company.Address,
            company.ContactEmail,
            company.ContactPhone,
            company.Website
        });
    }

    [HttpGet("/api/v1/tracks")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTracks(CancellationToken cancellationToken)
    {
        var tracks = await _context.AcademicTracks
            .OrderBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Name,
                t.Description
            })
            .ToListAsync(cancellationToken);

        return Ok(tracks);
    }
}

