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
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
            return Result<AuthResponse>.Failure("Identifiants invalides ou compte inactif.");

        // ASP.NET Identity uses PBKDF2 with HMAC-SHA256 by default
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
            return Result<AuthResponse>.Failure("Identifiants invalides.");

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? UserRole.Etudiant;

        var (accessToken, accessExpiresAt) = GenerateJwtToken(user, primaryRole);
        var (refreshToken, rawRefreshToken) = GenerateRefreshToken(user.Id, ipAddress);

        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await PurgeExpiredTokensAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.FullName,
            primaryRole,
            user.StudentNumber,
            user.AcademicTrackId);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt,
            userDto));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string token, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var existingRefreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == tokenHash, cancellationToken);

        if (existingRefreshToken == null)
            return Result<AuthResponse>.Failure("Refresh token introuvable.");

        if (existingRefreshToken.IsRevoked)
        {
            // Détection de réutilisation : un token déjà remplacé est rejoué.
            // On invalide toute la famille de tokens de l'utilisateur par précaution.
            await RevokeAllActiveTokensAsync(existingRefreshToken.UserId, ipAddress, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<AuthResponse>.Failure("Réutilisation d'un refresh token détectée. Toutes les sessions ont été révoquées.");
        }

        if (existingRefreshToken.IsExpired)
            return Result<AuthResponse>.Failure("Ce refresh token a expiré. Veuillez vous reconnecter.");

        var user = await _userManager.FindByIdAsync(existingRefreshToken.UserId);
        if (user == null || !user.IsActive)
            return Result<AuthResponse>.Failure("Utilisateur introuvable ou inactif.");

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? UserRole.Etudiant;

        // Rotation : révoquer l'ancien token et en émettre un nouveau.
        var (newRefreshToken, rawRefreshToken) = GenerateRefreshToken(user.Id, ipAddress);
        existingRefreshToken.Revoke(ipAddress, HashToken(rawRefreshToken));

        await _context.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await PurgeExpiredTokensAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var (newAccessToken, accessExpiresAt) = GenerateJwtToken(user, primaryRole);

        var userDto = new UserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.FullName,
            primaryRole,
            user.StudentNumber,
            user.AcademicTrackId);

        return Result<AuthResponse>.Success(new AuthResponse(
            newAccessToken,
            accessExpiresAt,
            rawRefreshToken,
            newRefreshToken.ExpiresAt,
            userDto));
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
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            return Result<UserDto>.Failure("Un utilisateur avec cette adresse email existe déjà.");

        if (!await _roleManager.RoleExistsAsync(role))
            return Result<UserDto>.Failure($"Le rôle '{role}' n'existe pas dans le système.");

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
        var dto = new UserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.FullName,
            roles.FirstOrDefault() ?? "None",
            user.StudentNumber,
            user.AcademicTrackId);

        return Result<UserDto>.Success(dto);
    }

    public async Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);
        var resultList = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            resultList.Add(new UserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.FullName,
                roles.FirstOrDefault() ?? "None",
                user.StudentNumber,
                user.AcademicTrackId));
        }

        return Result<List<UserDto>>.Success(resultList);
    }

    public async Task<Result> UpdateUserRoleAsync(string userId, string newRole, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Failure("Utilisateur introuvable.");

        if (!await _roleManager.RoleExistsAsync(newRole))
            return Result.Failure($"Le rôle '{newRole}' est invalide.");

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, newRole);

        return Result.Success();
    }

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

    private async Task PurgeExpiredTokensAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddDays(-7);
        var staleTokens = await _context.RefreshTokens
            .Where(r => r.ExpiresAt < threshold || (r.RevokedAt != null && r.RevokedAt < threshold))
            .ToListAsync(cancellationToken);

        if (staleTokens.Count > 0)
        {
            _context.RefreshTokens.RemoveRange(staleTokens);
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
