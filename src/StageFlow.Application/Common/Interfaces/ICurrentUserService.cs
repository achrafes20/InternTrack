namespace StageFlow.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserEmail { get; }
    string? UserRole { get; }
    string? AcademicTrackId { get; }
    bool IsAuthenticated { get; }
}
