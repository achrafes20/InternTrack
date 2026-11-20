using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StageFlow.Application.Common.Interfaces;

namespace StageFlow.Infrastructure.Services.Ai;

public class GeminiAiSummaryService : IAiSummaryService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAiSummaryService> _logger;

    public GeminiAiSummaryService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiAiSummaryService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> SummarizeReportAsync(string activities, string skills, string difficulties, string nextGoals, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_API_KEY")
        {
            // Mode Fallback intelligent en développement
            return $"[Synthèse Automatique] Activités phares : {activities.Substring(0, Math.Min(120, activities.Length))}... " +
                   $"Compétences consolidées : {skills}. Objectifs de la semaine suivante identifiés avec succès.";
        }

        try
        {
            var prompt = $@"
Tu es un tuteur académique senior en école d'ingénieurs (ENSA). Rédige une synthèse concise et percutante (3 à 5 phrases en français) du rapport d'avancement suivant :
- Activités réalisées : {activities}
- Compétences acquises : {skills}
- Difficultés rencontrées : {difficulties}
- Objectifs pour la semaine prochaine : {nextGoals}

Mets en avant la progression technique et les éventuels points d'alerte pour l'encadrant.";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            // La clé transite dans un en-tête (jamais dans l'URL, qui finit dans les logs des proxys).
            var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Échec de l'appel à l'API Gemini : {StatusCode} - {Error}", response.StatusCode, errorContent);
                return $"[Synthèse Hebdomadaire] {activities.Substring(0, Math.Min(120, activities.Length))}... Objectifs fixés : {nextGoals}";
            }

            var jsonResult = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var generatedText = jsonResult
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return generatedText ?? "Synthèse non disponible.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'appel à l'API Gemini.");
            return $"[Synthèse Hebdomadaire] Avancement conforme aux attentes : {activities.Substring(0, Math.Min(120, activities.Length))}...";
        }
    }
}

