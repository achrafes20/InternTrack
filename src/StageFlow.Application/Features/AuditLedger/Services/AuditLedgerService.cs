using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Common.Models;
using StageFlow.Application.Features.AuditLedger.DTOs;

namespace StageFlow.Application.Features.AuditLedger.Services;

public interface IAuditLedgerService
{
    Task<PaginatedList<AuditLogDto>> GetAuditLogsAsync(string? entityName, string? action, int pageNumber, int pageSize, CancellationToken cancellationToken);
}

public class AuditLedgerService : IAuditLedgerService
{
    private readonly IApplicationDbContext _context;

    public AuditLedgerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AuditLogDto>> GetAuditLogsAsync(string? entityName, string? action, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(a => a.EntityName.ToLower() == entityName.ToLower());

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.ToLower() == action.ToLower());

        var dtoQuery = query.OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditLogDto(
                a.Id,
                a.EntityName,
                a.EntityId,
                a.Action,
                a.UserId,
                a.UserEmail,
                a.Timestamp,
                a.Changes));

        return await PaginatedList<AuditLogDto>.CreateAsync(dtoQuery, pageNumber, pageSize, cancellationToken);
    }
}

