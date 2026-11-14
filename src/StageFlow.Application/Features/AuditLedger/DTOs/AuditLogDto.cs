namespace StageFlow.Application.Features.AuditLedger.DTOs;

public record AuditLogDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string? UserId,
    string? UserEmail,
    DateTime Timestamp,
    string? Changes);

