using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Entities;

namespace StageFlow.Infrastructure.Persistence.Interceptors;

public class AuditLedgerInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditLedgerInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AuditChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AuditChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AuditChanges(DbContext? context)
    {
        if (context == null) return;

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog && 
                        e.Entity is not RefreshToken &&
                        !e.Entity.GetType().Namespace!.StartsWith("Microsoft.AspNetCore.Identity") &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        if (!entries.Any()) return;

        var userId = _currentUserService.UserId;
        var userEmail = _currentUserService.UserEmail;

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
            var entityId = idProperty?.CurrentValue?.ToString() ?? "CompositeOrUnknown";
            var action = entry.State.ToString();

            var changesDict = new Dictionary<string, object?>();

            if (entry.State == EntityState.Modified)
            {
                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    changesDict[prop.Metadata.Name] = new
                    {
                        Old = prop.OriginalValue,
                        New = prop.CurrentValue
                    };
                }
            }

            var auditLog = new AuditLog(
                entityName: entityName,
                entityId: entityId,
                action: action,
                userId: userId,
                userEmail: userEmail,
                changes: changesDict.Any() ? JsonSerializer.Serialize(changesDict) : null
            );

            context.Set<AuditLog>().Add(auditLog);
        }
    }
}

