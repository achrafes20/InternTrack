using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Auth.Models;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Persistence;

namespace StageFlow.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly StageFlowDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        StageFlowDbContext context,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<Result<AuthResponse>> AuthenticateAsync(string email, string password, string? ipAddress, CancellationToken cancellationToken)
    {
        // Message identique pour tous les échecs (compte inconnu, inactif, mot de passe faux, verrouillé) :
        // un attaquant ne peut pas savoir si un email correspond à un compte.
        const string genericFailure = "Identifiants invalides ou compte temporairement verrouillé. Réessayez dans quelques minutes.";

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
            return Result<AuthResponse>.Failure(genericFailure);

        // Verrouillage par compte (complète le rate limiting par IP, inefficace contre une attaque distribuée).
        if (await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponse>.Failure(genericFailure);

        // ASP.NET Identity utilise PBKDF2 (HMAC-SHA256) par défaut.
        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthResponse>.Failure(genericFailure);
        }

        if (await _userManager.GetAccessFailedCountAsync(user) > 0)
            await _userManager.ResetAccessFailedCountAsync(user);

        var (refreshToken, rawRefreshToken) = GenerateRefreshToken(user.Id, ipAddress);
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await PurgeStaleTokensAsync(cancellationToken);

        return Result<AuthResponse>.Success(await BuildAuthResponseAsync(user, refreshToken, rawRefreshToken));
    }

    public Task<Result<AuthResponse>> RefreshTokenAsync(string token, string? ipAddress, CancellationToken cancellationToken)
        => RefreshTokenCoreAsync(token, ipAddress, isRetry: false, cancellationToken);

    private async Task<Result<AuthResponse>> RefreshTokenCoreAsync(string token, string? ipAddress, bool isRetry, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var existingRefreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == tokenHash, cancellationToken);

        if (existingRefreshToken == null)
            return Result<AuthResponse>.Failure("Refresh token introuvable.");

        if (existingRefreshToken.IsExpired)
            return Result<AuthResponse>.Failure("Ce refresh token a expiré. Veuillez vous reconnecter.");

        var isGraceReuse = false;
        if (existingRefreshToken.IsRevoked)
        {
            // Tolérance : plusieurs onglets (ou une requête rejouée par le réseau) peuvent présenter le même
            // token quelques secondes après sa rotation. Ce n'est pas un vol tant que le token de remplacement
            // est toujours actif : on émet alors une nouvelle paire sans révoquer la famille.
            isGraceReuse = await IsWithinRotationGraceAsync(existingRefreshToken, cancellationToken);

            if (!isGraceReuse)
            {
                // Rejeu réel d'un token remplacé : toute la famille de tokens de l'utilisateur est invalidée.
                await RevokeAllActiveTokensAsync(existingRefreshToken.UserId, ipAddress, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return Result<AuthResponse>.Failure("Réutilisation d'un refresh token détectée. Toutes les sessions ont été révoquées.");
            }
        }

        var user = await _userManager.FindByIdAsync(existingRefreshToken.UserId);
        if (user == null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponse>.Failure("Utilisateur introuvable, inactif ou verrouillé.");

        var (newRefreshToken, rawRefreshToken) = GenerateRefreshToken(user.Id, ipAddress);
        if (!isGraceReuse)
        {
            // Rotation : l'ancien token est révoqué et pointe vers son remplaçant.
            existingRefreshToken.Revoke(ipAddress, HashToken(rawRefreshToken));
        }

        await _context.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (!isRetry)
        {
            // Deux rafraîchissements simultanés du même token : l'autre requête l'a déjà fait tourner.
            // On recharge l'état et on rejoue une fois : le cas tombe alors dans la période de tolérance.
            _context.ChangeTracker.Clear();
            return await RefreshTokenCoreAsync(token, ipAddress, isRetry: true, cancellationToken);
        }

        return Result<AuthResponse>.Success(await BuildAuthResponseAsync(user, newRefreshToken, rawRefreshToken));
    }

    private async Task<bool> IsWithinRotationGraceAsync(RefreshToken revokedToken, CancellationToken cancellationToken)
    {
        if (revokedToken.ReplacedByToken == null || revokedToken.RevokedAt == null)
            return false; // Révoqué par une déconnexion ou une révocation globale : jamais toléré.

        var grace = TimeSpan.FromSeconds(Math.Max(0, _jwtSettings.RefreshTokenReuseGraceSeconds));
        if (DateTime.UtcNow - revokedToken.RevokedAt.Value > grace)
            return false;

        var replacement = await _context.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Token == revokedToken.ReplacedByToken, cancellationToken);

        return replacement is { IsActive: true };
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, RefreshToken refreshToken, string rawRefreshToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? UserRole.Etudiant;
        var (accessToken, accessExpiresAt) = GenerateJwtToken(user, primaryRole);

        var userDto = new UserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.FullName,
            primaryRole,
            user.StudentNumber,
            user.AcademicTrackId,
            user.IsActive);

        return new AuthResponse(accessToken, accessExpiresAt, rawRefreshToken, refreshToken.ExpiresAt, userDto);
    }

    public async Task<Result> RevokeTokenAsync(string token, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var existingRefreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == tokenHash, cancellationToken);

        if (existingRefreshToken == null)
            return Result.Failure("Token inexistant.");

        if (!existingRefreshToken.IsActive)
            return Result.Failure("Le token est déjà inactif.");

        existingRefreshToken.Revoke(ipAddress);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<UserDto>> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string role,
        string? studentNumber,
        Guid? academicTrackId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Result<UserDto>.Failure("L'email, le prénom et le nom sont obligatoires.");

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            return Result<UserDto>.Failure("Un utilisateur avec cette adresse email existe déjà.");

        if (!await _roleManager.RoleExistsAsync(role))
            return Result<UserDto>.Failure($"Le rôle '{role}' n'existe pas dans le système.");

        var trackValidation = await ValidateTrackForRoleAsync(role, academicTrackId, cancellationToken);
        if (trackValidation.IsFailure)
            return Result<UserDto>.Failure(trackValidation.Error);

        if (role == UserRole.Etudiant && string.IsNullOrWhiteSpace(studentNumber))
            return Result<UserDto>.Failure("Le code Apogée (numéro étudiant) est obligatoire pour un élève ingénieur.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            StudentNumber = studentNumber,
            AcademicTrackId = academicTrackId,
            EmailConfirmed = true
        };

        var creationResult = await _userManager.CreateAsync(user, password);
        if (!creationResult.Succeeded)
        {
            var errors = string.Join("; ", creationResult.Errors.Select(e => e.Description));
            return Result<UserDto>.Failure($"Échec de création du compte : {errors}");
        }

        await _userManager.AddToRoleAsync(user, role);

        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            role,
            user.StudentNumber,
            user.AcademicTrackId);

        return Result<UserDto>.Success(userDto);
    }

    public async Task<Result<UserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result<UserDto>.Failure("Utilisateur introuvable.");

        var roles = await _userManager.GetRolesAsync(user);
        string? trackCode = null;
        if (user.AcademicTrackId.HasValue)
        {
            trackCode = await _context.AcademicTracks.AsNoTracking()
                .Where(t => t.Id == user.AcademicTrackId.Value)
                .Select(t => t.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result<UserDto>.Success(ToDto(user, roles.FirstOrDefault() ?? "None", trackCode));
    }

    public async Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken)
        => Result<List<UserDto>>.Success(await LoadUsersAsync(_userManager.Users, cancellationToken));

    public async Task<List<UserDto>> GetUsersInRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var roleNames = roles.ToList();
        var userIds = from ur in _context.UserRoles
                      join r in _context.Roles on ur.RoleId equals r.Id
                      where roleNames.Contains(r.Name!)
                      select ur.UserId;

        return await LoadUsersAsync(_userManager.Users.Where(u => userIds.Contains(u.Id)), cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();

        var users = await _userManager.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    public async Task<Result> UpdateUserRoleAsync(string userId, string newRole, Guid? academicTrackId, string currentUserId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure("Utilisateur introuvable.");

        if (!await _roleManager.RoleExistsAsync(newRole))
            return Result.Failure($"Le rôle '{newRole}' est invalide.");

        // Un administrateur ne peut pas se retirer lui-même ses droits (évite de verrouiller la plateforme).
        if (userId == currentUserId && newRole != UserRole.Admin)
            return Result.Failure("Vous ne pouvez pas retirer votre propre rôle d'administrateur.");

        var effectiveTrackId = academicTrackId ?? user.AcademicTrackId;
        var trackValidation = await ValidateTrackForRoleAsync(newRole, effectiveTrackId, cancellationToken);
        if (trackValidation.IsFailure)
            return trackValidation;

        if (user.AcademicTrackId != effectiveTrackId)
        {
            user.AcademicTrackId = effectiveTrackId;
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
                return Result.Failure(string.Join("; ", update.Errors.Select(e => e.Description)));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count == 1 && currentRoles[0] == newRole)
            return Result.Success();

        var removal = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removal.Succeeded)
            return Result.Failure(string.Join("; ", removal.Errors.Select(e => e.Description)));

        var addition = await _userManager.AddToRoleAsync(user, newRole);
        if (!addition.Succeeded)
            return Result.Failure(string.Join("; ", addition.Errors.Select(e => e.Description)));

        // Les sessions en cours portent l'ancien rôle dans leur JWT : on force une reconnexion.
        await RevokeAllActiveTokensAsync(user.Id, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UnlockUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure("Utilisateur introuvable.");

        var unlock = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!unlock.Succeeded)
            return Result.Failure(string.Join("; ", unlock.Errors.Select(e => e.Description)));

        await _userManager.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    /// <summary>Les étudiants et responsables de filière doivent être rattachés à une filière existante.</summary>
    private async Task<Result> ValidateTrackForRoleAsync(string role, Guid? academicTrackId, CancellationToken cancellationToken)
    {
        var requiresTrack = role is UserRole.Etudiant or UserRole.ResponsableFiliere;

        if (requiresTrack && !academicTrackId.HasValue)
            return Result.Failure("Une filière de rattachement est obligatoire pour ce rôle.");

        if (academicTrackId.HasValue && !await _context.AcademicTracks.AnyAsync(t => t.Id == academicTrackId.Value, cancellationToken))
            return Result.Failure("La filière spécifiée n'existe pas.");

        return Result.Success();
    }

    private async Task<List<UserDto>> LoadUsersAsync(IQueryable<ApplicationUser> usersQuery, CancellationToken cancellationToken)
    {
        // Chargement groupé des rôles et filières : évite une requête par utilisateur (N+1).
        var users = await usersQuery.AsNoTracking().OrderBy(u => u.LastName).ToListAsync(cancellationToken);
        var ids = users.Select(u => u.Id).ToList();

        var roleByUser = (await (from ur in _context.UserRoles
                                 join r in _context.Roles on ur.RoleId equals r.Id
                                 where ids.Contains(ur.UserId)
                                 select new { ur.UserId, r.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().Name ?? "None");

        var trackCodes = await _context.AcademicTracks.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Code, cancellationToken);

        return users
            .Select(u => ToDto(
                u,
                roleByUser.GetValueOrDefault(u.Id, "None"),
                u.AcademicTrackId.HasValue ? trackCodes.GetValueOrDefault(u.AcademicTrackId.Value) : null))
            .ToList();
    }

    private static UserDto ToDto(ApplicationUser user, string role, string? trackCode) => new(
        user.Id,
        user.Email!,
        user.FirstName,
        user.LastName,
        user.FullName,
        role,
        user.StudentNumber,
        user.AcademicTrackId,
        user.IsActive,
        trackCode,
        user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow);

    public async Task<bool> IsUserInRoleAsync(string userId, string role, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(ApplicationUser user, string role)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.AcademicTrackId.HasValue)
        {
            claims.Add(new Claim("AcademicTrackId", user.AcademicTrackId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(token), expiresAt);
    }

    private (RefreshToken Token, string RawToken) GenerateRefreshToken(string userId, string? ipAddress)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var rawToken = Convert.ToBase64String(randomBytes);

        // Seul le hash du token est persisté : une fuite de la base ne permet pas
        // de rejouer les sessions. Le token brut n'est renvoyé qu'au client.
        var refreshToken = new RefreshToken(
            token: HashToken(rawToken),
            userId: userId,
            expiresAt: DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            createdByIp: ipAddress);

        return (refreshToken, rawToken);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>
    /// Supprime les tokens expirés ou révoqués depuis plus de 7 jours. Sur PostgreSQL, un DELETE en masse
    /// est utilisé : il n'entre pas en conflit de concurrence avec une autre connexion qui purgerait en même temps.
    /// </summary>
    private async Task PurgeStaleTokensAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddDays(-7);
        var stale = _context.RefreshTokens
            .Where(r => r.ExpiresAt < threshold || (r.RevokedAt != null && r.RevokedAt < threshold));

        if (_context.Database.IsNpgsql())
        {
            await stale.ExecuteDeleteAsync(cancellationToken);
            return;
        }

        // La base en mémoire (tests) ne supporte pas ExecuteDelete.
        var staleTokens = await stale.ToListAsync(cancellationToken);
        if (staleTokens.Count > 0)
        {
            _context.RefreshTokens.RemoveRange(staleTokens);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RevokeAllActiveTokensAsync(string userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.Revoke(ipAddress);
        }
    }
}
