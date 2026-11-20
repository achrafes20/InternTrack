using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StageFlow.Api.IntegrationTests;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Environnement de développement : active la clé JWT de dev et le seeding de démonstration.
            builder.UseEnvironment("Development");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("InMemoryDatabaseName", $"StageFlowTests-{Guid.NewGuid()}");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("Database:SeedDemoData", "true");
            builder.UseSetting("Smtp:Enabled", "false");
        });
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOkWithHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Login_WithDefaultAdminCredentials_ShouldReturnJwtAccessToken()
    {
        var client = _factory.CreateClient();

        var loginPayload = new
        {
            email = "admin@ensa.ma",
            password = "Admin@123456"
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.TryGetProperty("accessToken", out var accessToken).Should().BeTrue();
        accessToken.GetString().Should().NotBeNullOrEmpty();

        // Vérification du cookie HttpOnly
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookieValue = cookies!.FirstOrDefault() ?? string.Empty;
        cookieValue.Should().Contain("stageflow_refresh_token");
        cookieValue.ToLower().Should().Contain("httponly");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var loginPayload = new
        {
            email = "admin@ensa.ma",
            password = "WrongPassword999!"
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInternships_WithoutToken_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/internships");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDashboardStats_AsStudent_ShouldReturnScopedStats()
    {
        var client = _factory.CreateClient();

        var loginPayload = new
        {
            email = "etudiant.achraf@ensa.ma",
            password = "Student@123456"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = json.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        stats.TryGetProperty("totalInternships", out var total).Should().BeTrue();
        total.GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetAuditLogs_AsStudent_ShouldReturnForbidden()
    {
        var client = _factory.CreateClient();

        var loginPayload = new
        {
            email = "etudiant.achraf@ensa.ma",
            password = "Student@123456"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginPayload);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = json.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/audit");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
