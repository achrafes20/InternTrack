using StageFlow.Application.Features.Auth.Models;
using StageFlow.Domain.Common;

namespace StageFlow.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<Result<AuthResponse>> AuthenticateAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task<Result> RevokeTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);
    Task<Result<UserDto>> CreateUserAsync(string email, string password, string firstName, string lastName, string role, string? studentNumber, Guid? academicTrackId, int? graduationYear, CancellationToken cancellationToken);
    Task<Result<UserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
    Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<List<UserDto>> GetUsersInRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken);

    /// <summary>Résout en une seule requête les noms complets d'un ensemble d'utilisateurs (id → nom).</summary>
    Task<Dictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken);

    Task<Result> UpdateUserRoleAsync(string userId, string newRole, Guid? academicTrackId, int? graduationYear, string currentUserId, CancellationToken cancellationToken);
    Task<bool> IsUserInRoleAsync(string userId, string role, CancellationToken cancellationToken);

    /// <summary>Lève le verrouillage consécutif à des échecs de connexion répétés.</summary>
    Task<Result> UnlockUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Désactive (ou réactive) un compte. Une désactivation ferme immédiatement ses sessions.</summary>
    Task<Result> SetUserActiveAsync(string userId, bool isActive, string currentUserId, CancellationToken cancellationToken);

    /// <summary>Changement de mot de passe par l'utilisateur connecté (mot de passe actuel exigé).</summary>
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Envoie un lien de réinitialisation si l'email correspond à un compte actif. Ne révèle jamais
    /// si le compte existe : le résultat est toujours un succès pour l'appelant.
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken);
}
