using StageFlow.Application.Features.Auth.Models;
using StageFlow.Domain.Common;

namespace StageFlow.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<Result<AuthResponse>> AuthenticateAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task<Result> RevokeTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task<Result<UserDto>> CreateUserAsync(string email, string password, string firstName, string lastName, string role, string? studentNumber, Guid? academicTrackId, CancellationToken cancellationToken);
    Task<Result<UserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
    Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<Result> UpdateUserRoleAsync(string userId, string newRole, CancellationToken cancellationToken);
    Task<bool> IsUserInRoleAsync(string userId, string role, CancellationToken cancellationToken);
}

