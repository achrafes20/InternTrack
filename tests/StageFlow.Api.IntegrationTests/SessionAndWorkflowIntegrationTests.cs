using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StageFlow.Api.IntegrationTests;

public class SessionAndWorkflowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SessionAndWorkflowIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("InMemoryDatabaseName", $"StageFlowTests-{Guid.NewGuid()}");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("Database:SeedDemoData", "true");
            builder.UseSetting("Smtp:Enabled", "false");
            builder.UseSetting("Storage:RootPath", Path.Combine(Path.GetTempPath(), "stageflow-tests", Guid.NewGuid().ToString("N")));
        });
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.First(c => c.StartsWith("stageflow_refresh_token=", StringComparison.Ordinal));
        return cookie.Split(';')[0];
    }

    private static HttpRequestMessage RefreshRequest(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    private async Task<(HttpClient Client, JsonElement Login)> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return (client, json);
    }

    [Fact]
    public async Task RefreshToken_IsRotated_AndReplayRevokesTheWholeSession()
    {
        // Sans période de tolérance multi-onglets : tout rejeu d'un token remplacé est traité comme un vol.
        var noGrace = _factory.WithWebHostBuilder(b => b.UseSetting("JwtSettings:RefreshTokenReuseGraceSeconds", "0"));
        var client = noGrace.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "prof.tazi@ensa.ma", password = "Prof@123456" });
        var firstCookie = ExtractRefreshCookie(login);

        var firstRefresh = await client.SendAsync(RefreshRequest(firstCookie));
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondCookie = ExtractRefreshCookie(firstRefresh);
        secondCookie.Should().NotBe(firstCookie, "le refresh token doit tourner à chaque utilisation");

        // Rejeu de l'ancien token : refusé, et la famille de tokens est révoquée.
        var replay = await client.SendAsync(RefreshRequest(firstCookie));
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var afterReplay = await client.SendAsync(RefreshRequest(secondCookie));
        afterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "le token légitime est révoqué après détection d'un rejeu");
    }

    [Fact]
    public async Task ScheduleDefense_WithNumericJuryRole_IsRejected()
    {
        var (client, _) = await LoginAsync("responsable.gi@ensa.ma", "Resp@123456");

        var internships = await client.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var target = internships.EnumerateArray().First(i => i.GetProperty("statusName").GetString() == "EnCours");

        var response = await client.PostAsJsonAsync("/api/v1/defenses", new
        {
            internshipId = target.GetProperty("id").GetString(),
            scheduledAt = DateTime.UtcNow.AddDays(30),
            durationMinutes = 45,
            classroom = "Salle Test",
            juryMembers = new object[]
            {
                new { teacherId = "x", role = 0 },
                new { teacherId = "y", role = 1 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DepartmentHead_CanListTeachers_ButStudentCannot()
    {
        var (head, _) = await LoginAsync("responsable.gi@ensa.ma", "Resp@123456");
        var teachers = await head.GetFromJsonAsync<JsonElement>("/api/v1/teachers");
        teachers.EnumerateArray().Select(t => t.GetProperty("fullName").GetString())
            .Should().Contain("Mohamed Tazi");

        var (student, _) = await LoginAsync("etudiant.fatima@ensa.ma", "Student@123456");
        (await student.GetAsync("/api/v1/teachers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Internships_ExposeStudentNamesAndConventionStatus()
    {
        var (client, _) = await LoginAsync("etudiant.achraf@ensa.ma", "Student@123456");

        var internships = await client.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var active = internships.EnumerateArray().First(i => i.GetProperty("statusName").GetString() == "EnCours");

        active.GetProperty("studentName").GetString().Should().Be("Achraf Bennani");
        active.GetProperty("convention").GetProperty("isSignedByStudent").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AuditLogs_PageSizeIsCapped()
    {
        var (admin, _) = await LoginAsync("admin@ensa.ma", "Admin@123456");

        var page = await admin.GetFromJsonAsync<JsonElement>("/api/v1/audit?pageNumber=0&pageSize=100000");

        page.GetProperty("pageNumber").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(100);
    }
}
