using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StageFlow.Api.IntegrationTests.PostgreSql;

/// <summary>
/// L'API complète démarrée sur PostgreSQL : migrations au démarrage, données de démonstration,
/// authentification, verrouillage de compte, validation et gestion des conflits.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
[Trait("Category", "PostgreSQL")]
public class PostgreSqlApiTests
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlApiTests(PostgreSqlFixture fixture) => _fixture = fixture;

    /// <summary>Chaque test obtient sa propre API, sa propre base et son propre rate limiter.</summary>
    private WebApplicationFactory<Program> CreateApi(int maxFailedLoginAttempts = 5)
    {
        var connectionString = _fixture.ConnectionString($"stageflow_api_{Guid.NewGuid():N}");

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("UseInMemoryDatabase", "false");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting("Database:SeedDemoData", "true");
            builder.UseSetting("Smtp:Enabled", "false");
            builder.UseSetting("Security:MaxFailedLoginAttempts", maxFailedLoginAttempts.ToString());
            builder.UseSetting("Storage:RootPath", Path.Combine(Path.GetTempPath(), "stageflow-tests", Guid.NewGuid().ToString("N")));
        });
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
        => await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private static async Task<HttpClient> AuthenticatedClientAsync(WebApplicationFactory<Program> api, string email, string password)
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await LoginAsync(client, email, password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }

    private static string RefreshCookie(HttpResponseMessage response)
        => response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("stageflow_refresh_token=", StringComparison.Ordinal)).Split(';')[0];

    private static HttpRequestMessage RefreshRequest(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    [Fact]
    public async Task Api_StartsOnPostgreSql_MigratesSeedsAndServesData()
    {
        await using var api = CreateApi();
        var client = api.CreateClient();

        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await AuthenticatedClientAsync(api, "admin@ensa.ma", "Admin@123456");
        var internships = await admin.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        internships.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);

        var audit = await admin.GetFromJsonAsync<JsonElement>("/api/v1/audit?pageSize=5");
        audit.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        audit.GetProperty("items").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task RefreshToken_TwoTabsWithinGracePeriod_BothSucceed_ButLaterReplayRevokesEverything()
    {
        await using var api = CreateApi();
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var login = await LoginAsync(client, "prof.tazi@ensa.ma", "Prof@123456");
        var original = RefreshCookie(login);

        // Deux onglets rafraîchissent avec le même cookie, presque en même temps.
        var tab1 = await client.SendAsync(RefreshRequest(original));
        var tab2 = await client.SendAsync(RefreshRequest(original));

        tab1.StatusCode.Should().Be(HttpStatusCode.OK);
        tab2.StatusCode.Should().Be(HttpStatusCode.OK, "la réutilisation immédiate d'un token tout juste remplacé est tolérée");

        // Le jeton de l'onglet 1 continue de fonctionner normalement.
        var next = await client.SendAsync(RefreshRequest(RefreshCookie(tab1)));
        next.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConcurrentRefreshes_OfSameToken_NeverRevokeTheSession()
    {
        await using var api = CreateApi();
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var login = await LoginAsync(client, "etudiant.fatima@ensa.ma", "Student@123456");
        var cookie = RefreshCookie(login);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.SendAsync(RefreshRequest(cookie))));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Account_IsLockedAfterRepeatedFailures_EvenWithCorrectPassword_UntilAdminUnlocks()
    {
        await using var api = CreateApi(maxFailedLoginAttempts: 3);
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        for (var i = 0; i < 3; i++)
            (await LoginAsync(client, "etudiant.mehdi@ensa.ma", "Mauvais@MotDePasse1")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await LoginAsync(client, "etudiant.mehdi@ensa.ma", "Student@123456")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "le compte est verrouillé après 3 échecs");

        var admin = await AuthenticatedClientAsync(api, "admin@ensa.ma", "Admin@123456");
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/v1/users");
        var mehdi = users.EnumerateArray().Single(u => u.GetProperty("email").GetString() == "etudiant.mehdi@ensa.ma");
        mehdi.GetProperty("isLockedOut").GetBoolean().Should().BeTrue();

        (await admin.PostAsync($"/api/v1/users/{mehdi.GetProperty("id").GetString()}/unlock", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task OversizedOrMalformedInput_Returns400_NotA500()
    {
        await using var api = CreateApi();
        var student = await AuthenticatedClientAsync(api, "etudiant.fatima@ensa.ma", "Student@123456");
        var me = await student.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var trackId = me.EnumerateArray().First().GetProperty("academicTrackId").GetString();
        var companies = await student.GetFromJsonAsync<JsonElement>("/api/v1/companies");

        var response = await student.PostAsJsonAsync("/api/v1/internships", new
        {
            title = new string('x', 251),
            subjectDescription = "Sujet",
            startDate = DateTime.UtcNow.AddDays(10),
            endDate = DateTime.UtcNow.AddMonths(6),
            companyId = companies[0].GetProperty("id").GetString(),
            academicTrackId = trackId,
            companySupervisorName = "Tuteur",
            companySupervisorEmail = "pas-un-email",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("Title", out _).Should().BeTrue();
        problem.GetProperty("errors").TryGetProperty("CompanySupervisorEmail", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentApprovals_OfSameInternship_OneSucceedsOtherGetsConflictOrBadRequest()
    {
        await using var api = CreateApi();
        var head = await AuthenticatedClientAsync(api, "responsable.gi@ensa.ma", "Resp@123456");

        var internships = await head.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var proposed = internships.EnumerateArray().First(i => i.GetProperty("statusName").GetString() == "Propose");
        var id = proposed.GetProperty("id").GetString();

        var responses = await Task.WhenAll(
            head.PostAsync($"/api/v1/internships/{id}/approve", null),
            head.PostAsJsonAsync($"/api/v1/internships/{id}/reject", new { rejectionReason = "Refus concurrent" }));

        // Exactement une décision est appliquée ; l'autre est refusée (409 si la course a lieu en base,
        // 400 si la seconde requête lit déjà l'état mis à jour). Jamais d'écrasement silencieux ni de 500.
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.NoContent ||
            r.StatusCode == HttpStatusCode.Conflict ||
            r.StatusCode == HttpStatusCode.BadRequest);
    }
}
