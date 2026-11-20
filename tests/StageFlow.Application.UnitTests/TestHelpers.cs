using Microsoft.EntityFrameworkCore;
using Moq;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Application.Features.Auth.Models;
using StageFlow.Domain.Common;
using StageFlow.Domain.Entities;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Persistence;

namespace StageFlow.Application.UnitTests;

internal static class TestHelpers
{
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
