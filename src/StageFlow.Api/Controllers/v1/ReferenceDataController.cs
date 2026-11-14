using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Entities;

namespace StageFlow.Api.Controllers.v1;

public record CreateCompanyRequest(
    string Name,
    string Industry,
    string City,
    string Address,
    string ContactEmail,
    string ContactPhone,
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
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ProblemDetails { Title = "Entreprise invalide", Detail = "Le nom de l'entreprise est obligatoire.", Status = 400 });

        var company = new Company(
            request.Name.Trim(),
            request.Industry,
            request.City,
            request.Address,
            request.ContactEmail,
            request.ContactPhone,
            request.Website);

        await _context.Companies.AddAsync(company, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(company);
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

