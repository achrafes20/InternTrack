using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using StageFlow.Domain.Common;

namespace StageFlow.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Requête annulée par le client : inutile de polluer les logs avec une erreur.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Une exception non gérée s'est produite lors du traitement de la requête {Method} {Path}.",
                context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "Une erreur interne est survenue sur le serveur.",
            status = StatusCodes.Status500InternalServerError,
            // En production, aucun détail interne n'est exposé. En développement, le message
            // facilite le diagnostic mais reste accompagné d'un identifiant de corrélation.
            detail = _environment.IsDevelopment()
                ? exception.Message
                : "Une erreur interne est survenue. Contactez l'administrateur avec l'identifiant de corrélation.",
            traceId = context.TraceIdentifier,
            instance = context.Request.Path.Value
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
