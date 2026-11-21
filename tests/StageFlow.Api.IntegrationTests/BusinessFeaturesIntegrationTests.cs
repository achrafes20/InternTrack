using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StageFlow.Api.IntegrationTests;

/// <summary>Parcours HTTP des fonctionnalités de gestion : comptes, mots de passe, documents, offres.</summary>
public class BusinessFeaturesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"stageflow-tests-{Guid.NewGuid():N}");

    public BusinessFeaturesIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("UseInMemoryDatabase", "true");
            builder.UseSetting("InMemoryDatabaseName", $"StageFlowTests-{Guid.NewGuid()}");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("Database:SeedDemoData", "true");
            builder.UseSetting("Smtp:Enabled", "false");
            builder.UseSetting("Storage:RootPath", _storageRoot);
        });
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturns204_WithoutRevealingAccounts()
    {
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "inconnu@ensa.ma" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "admin@ensa.ma" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var badReset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = "admin@ensa.ma", token = "jeton-invalide", newPassword = "Nouveau@123456" });
        badReset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_RequiresTheCurrentPassword_AndTheNewOneWorks()
    {
        var client = await LoginAsync("etudiant.mehdi@ensa.ma", "Student@123456");

        (await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Faux@123456", newPassword = "Nouveau@123456" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Student@123456", newPassword = "Nouveau@123456" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await LoginAsync("etudiant.mehdi@ensa.ma", "Nouveau@123456");
    }

    [Fact]
    public async Task DeactivatedAccount_CannotLogIn_UntilReactivated()
    {
        var admin = await LoginAsync("admin@ensa.ma", "Admin@123456");
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/v1/users");
        var fatimaId = users.EnumerateArray().Single(u => u.GetProperty("email").GetString() == "etudiant.fatima@ensa.ma").GetProperty("id").GetString();

        (await admin.PutAsJsonAsync($"/api/v1/users/{fatimaId}/active", new { isActive = false })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = "etudiant.fatima@ensa.ma", password = "Student@123456" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await admin.PutAsJsonAsync($"/api/v1/users/{fatimaId}/active", new { isActive = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await LoginAsync("etudiant.fatima@ensa.ma", "Student@123456");
    }

    [Fact]
    public async Task Admin_CannotDeactivateTheirOwnAccount()
    {
        var admin = await LoginAsync("admin@ensa.ma", "Admin@123456");
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/v1/users");
        var adminId = users.EnumerateArray().Single(u => u.GetProperty("email").GetString() == "admin@ensa.ma").GetProperty("id").GetString();

        (await admin.PutAsJsonAsync($"/api/v1/users/{adminId}/active", new { isActive = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Student_UploadsThesis_ItIsListed_AndDownloadable_ButNonPdfIsRejected()
    {
        var student = await LoginAsync("etudiant.achraf@ensa.ma", "Student@123456");
        var internships = await student.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var running = internships.EnumerateArray().First(i => i.GetProperty("statusName").GetString() == "EnCours").GetProperty("id").GetString();

        using var notPdf = new MultipartFormDataContent { { new ByteArrayContent("pas un pdf"u8.ToArray()), "file", "memoire.pdf" } };
        (await student.PostAsync($"/api/v1/internships/{running}/documents/thesis", notPdf)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var pdf = new byte[2048];
        "%PDF-1.7\n"u8.CopyTo(pdf);
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "Mémoire PFE.pdf");

        var upload = await student.PostAsync($"/api/v1/internships/{running}/documents/thesis", form);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await upload.Content.ReadFromJsonAsync<JsonElement>();

        var listed = await student.GetFromJsonAsync<JsonElement>($"/api/v1/internships/{running}/documents");
        listed.EnumerateArray().Should().ContainSingle(d => d.GetProperty("kind").GetString() == "FinalThesis");

        var download = await student.GetAsync($"/api/v1/documents/{document.GetProperty("id").GetString()}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);

        // Un autre élève n'y a pas accès.
        var other = await LoginAsync("etudiant.fatima@ensa.ma", "Student@123456");
        (await other.GetAsync($"/api/v1/documents/{document.GetProperty("id").GetString()}/download")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DepartmentHead_CanEditAndDeactivateOffersOfTheirTrack()
    {
        var head = await LoginAsync("responsable.gi@ensa.ma", "Resp@123456");
        var offers = await head.GetFromJsonAsync<JsonElement>("/api/v1/offers?includeClosed=true&pageSize=100");
        var offer = offers.GetProperty("items").EnumerateArray().First();
        var id = offer.GetProperty("id").GetString();

        var update = await head.PutAsJsonAsync($"/api/v1/offers/{id}", new
        {
            title = "Offre mise à jour",
            description = offer.GetProperty("description").GetString(),
            requiredSkills = "C#, React",
            city = "Rabat",
            durationInWeeks = 20,
            monthlyStipend = 4000,
            expirationDate = DateTime.UtcNow.AddMonths(2)
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        (await head.PutAsJsonAsync($"/api/v1/offers/{id}/active", new { isActive = false })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var publicList = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/offers?pageSize=100");
        publicList.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("id").GetString()).Should().NotContain(id);

        (await head.PutAsJsonAsync($"/api/v1/offers/{id}/active", new { isActive = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AnomalyReport_IsForManagersOnly()
    {
        var admin = await LoginAsync("admin@ensa.ma", "Admin@123456");
        var response = await admin.GetAsync("/api/v1/anomalies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);

        var student = await LoginAsync("etudiant.achraf@ensa.ma", "Student@123456");
        (await student.GetAsync("/api/v1/anomalies")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CsvExport_IsAnExcelReadyFile_FilteredByAcademicYear()
    {
        var head = await LoginAsync("responsable.gi@ensa.ma", "Resp@123456");

        var years = await head.GetFromJsonAsync<JsonElement>("/api/v1/internships/academic-years");
        var year = years.EnumerateArray().First().GetString();
        year.Should().MatchRegex(@"^\d{4}-\d{4}$");

        var response = await head.GetAsync($"/api/v1/dashboard/export?academicYear={year}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain(year);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "BOM UTF-8 pour Excel");
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("Année universitaire;Promotion;");
        lines.Skip(1).Should().OnlyContain(l => l.StartsWith(year!));

        var stats = await head.GetFromJsonAsync<JsonElement>($"/api/v1/dashboard/stats?academicYear={year}");
        stats.GetProperty("academicYear").GetString().Should().Be(year);
        stats.GetProperty("yearlyStats").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DepartmentHead_CanCancelAProposedInternship_WithAReason()
    {
        var head = await LoginAsync("responsable.gi@ensa.ma", "Resp@123456");
        var internships = await head.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var proposed = internships.EnumerateArray().First(i => i.GetProperty("statusName").GetString() == "Propose").GetProperty("id").GetString();

        (await head.PostAsJsonAsync($"/api/v1/internships/{proposed}/cancel", new { reason = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await head.PostAsJsonAsync($"/api/v1/internships/{proposed}/cancel", new { reason = "Doublon de dossier" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await head.GetFromJsonAsync<JsonElement>($"/api/v1/internships/{proposed}");
        after.GetProperty("statusName").GetString().Should().Be("Annule");
        after.GetProperty("cancellationReason").GetString().Should().Be("Doublon de dossier");
    }

    [Fact]
    public async Task TutorEvaluationLink_UnknownToken_Returns404()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/api/v1/defenses/tutor-evaluation/jeton-inexistant")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullySignedSeedConvention_PublishesAFingerprint_MatchingTheDownloadedPdf()
    {
        var student = await LoginAsync("etudiant.achraf@ensa.ma", "Student@123456");
        var internships = await student.GetFromJsonAsync<JsonElement>("/api/v1/internships");
        var running = internships.EnumerateArray().First(i => i.GetProperty("statusName").GetString() == "EnCours");

        var pdf = await student.GetByteArrayAsync($"/api/v1/conventions/internship/{running.GetProperty("id").GetString()}/pdf");

        // Le jeton de vérification figure dans le QR code ; il est retrouvé ici via l'empreinte publiée.
        using var sha = System.Security.Cryptography.SHA256.Create();
        var fingerprint = Convert.ToHexString(sha.ComputeHash(pdf)).ToLowerInvariant();
        var again = await student.GetByteArrayAsync($"/api/v1/conventions/internship/{running.GetProperty("id").GetString()}/pdf");
        Convert.ToHexString(sha.ComputeHash(again)).ToLowerInvariant().Should().Be(fingerprint, "l'exemplaire signé est figé");
    }

    [Fact]
    public async Task CompanySigningLink_UnknownToken_Returns404()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/api/v1/conventions/company-signing/jeton-inexistant")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
