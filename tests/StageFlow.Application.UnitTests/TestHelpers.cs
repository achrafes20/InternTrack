using Microsoft.EntityFrameworkCore;
using Moq;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Auth.Models;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Persistence;
using StageFlow.Application.Features.Internships;

namespace StageFlow.Application.UnitTests;

/// <summary>Stockage de fichiers en mémoire pour les tests.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var key = $"{Guid.NewGuid():N}.{extension}";
        Files[key] = buffer.ToArray();
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        => Task.FromResult<Stream?>(Files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

internal static class TestHelpers
{
    public static readonly InternshipPolicy Policy = new();

    public static ITimeZoneService MoroccoTime { get; } =
        new StageFlow.Infrastructure.Services.Time.TimeZoneService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

    /// <summary>Contenu PDF minimal valide (signature %PDF-).</summary>
    public static byte[] FakePdf(int size = 1024)
    {
        var bytes = new byte[size];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        return bytes;
    }
    public static StageFlowDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StageFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StageFlowDbContext(options);
    }

    /// <summary>Service d'identité simulé exposant un jeu d'enseignants fixe.</summary>
    public static Mock<IIdentityService> CreateIdentityMock(params UserDto[] users)
    {
        var mock = new Mock<IIdentityService>();

        mock.Setup(s => s.GetUsersInRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> roles, CancellationToken _) =>
                users.Where(u => roles.Contains(u.Role)).ToList());

        mock.Setup(s => s.GetUserNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
                users.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id, u => u.FullName));

        mock.Setup(s => s.GetUserByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
            {
                var user = users.FirstOrDefault(u => u.Id == id);
                return user is null ? Result<UserDto>.Failure("Utilisateur introuvable.") : Result<UserDto>.Success(user);
            });

        return mock;
    }

    public static UserDto Teacher(string id, string fullName, string role = UserRole.Encadrant)
    {
        var parts = fullName.Split(' ', 2);
        return new UserDto(id, $"{id}@ensa.ma", parts[0], parts.Length > 1 ? parts[1] : string.Empty, fullName, role, null, null);
    }

    public static UserDto Student(string id, string fullName, Guid trackId)
    {
        var parts = fullName.Split(' ', 2);
        return new UserDto(id, $"{id}@ensa.ma", parts[0], parts.Length > 1 ? parts[1] : string.Empty, fullName, UserRole.Etudiant, "E0001", trackId);
    }

    /// <summary>Crée un stage « en cours » prêt à être planifié en soutenance.</summary>
    public static Internship CreateInProgressInternship(string studentId, Guid companyId, Guid trackId, string title = "Stage")
    {
        var internship = new Internship(title, "Desc", DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), studentId, companyId, trackId, "Tuteur", "tuteur@test.com");
        internship.SubmitForApproval(studentId);
        internship.Approve("admin");
        internship.StartInternship("admin");
        return internship;
    }
}
