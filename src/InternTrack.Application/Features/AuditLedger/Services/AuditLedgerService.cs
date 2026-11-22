using Microsoft.EntityFrameworkCore;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Application.Common.Models;
using InternTrack.Application.Features.AuditLedger.DTOs;

namespace InternTrack.Application.Features.AuditLedger.Services;

public record AuditLogFilter(
    string? EntityName = null,
    string? Action = null,
    string? User = null,
    DateTime? From = null,
    DateTime? To = null);

public interface IAuditLedgerService
{
    Task<PaginatedList<AuditLogDto>> GetAuditLogsAsync(AuditLogFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken);
}

public class AuditLedgerService : IAuditLedgerService
{
    private readonly IApplicationDbContext _context;

    public AuditLedgerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<AuditLogDto>> GetAuditLogsAsync(AuditLogFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            var entity = filter.EntityName.Trim().ToLower();
            query = query.Where(a => a.EntityName.ToLower().Contains(entity));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim().ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(filter.User))
        {
            var user = filter.User.Trim().ToLower();
            query = query.Where(a =>
                (a.UserEmail != null && a.UserEmail.ToLower().Contains(user)) ||
                (a.UserId != null && a.UserId.ToLower() == user));
        }

        if (filter.From.HasValue)
            query = query.Where(a => a.Timestamp >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(a => a.Timestamp <= filter.To.Value);

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
