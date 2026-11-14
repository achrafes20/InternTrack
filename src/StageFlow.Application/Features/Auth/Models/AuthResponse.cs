namespace StageFlow.Application.Features.Auth.Models;

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);

public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string Role,
    string? StudentNumber,
    Guid? AcademicTrackId);

