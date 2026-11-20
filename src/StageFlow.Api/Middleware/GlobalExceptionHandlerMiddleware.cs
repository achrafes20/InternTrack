using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StageFlow.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private const string DefenseSlotConstraint = "EX_Defenses_Classroom_Slot";

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
        catch (DbUpdateConcurrencyException ex)
        {
            // Concurrence optimiste (xmin) : un autre utilisateur a modifié la ressource entre-temps.
            _logger.LogWarning(ex, "Conflit de concurrence sur {Method} {Path}.", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict,
                "Conflit de modification",
                "Cette ressource vient d'être modifiée par un autre utilisateur. Rechargez la page puis réessayez.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && IsConstraintViolation(pg))
        {
            // Contrainte d'unicité ou d'exclusion : la base garantit l'intégrité même en cas de course.
            _logger.LogWarning(ex, "Violation de contrainte {Constraint} sur {Method} {Path}.",
                pg.ConstraintName, context.Request.Method, context.Request.Path);

            var detail = pg.SqlState == PostgresErrorCodes.ExclusionViolation && pg.ConstraintName == DefenseSlotConstraint
                ? "Cette salle est déjà réservée sur ce créneau horaire."
                : "Cette opération entre en conflit avec une donnée existante (doublon).";

            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflit", detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Une exception non gérée s'est produite lors du traitement de la requête {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            // En production, aucun détail interne n'est exposé. En développement, le message
            // facilite le diagnostic mais reste accompagné d'un identifiant de corrélation.
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Une erreur interne est survenue sur le serveur.",
                _environment.IsDevelopment()
                    ? ex.Message
                    : "Une erreur interne est survenue. Contactez l'administrateur avec l'identifiant de corrélation.");
        }
    }

    private static bool IsConstraintViolation(PostgresException pg)
        => pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation;

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted)
            return Task.CompletedTask;

        context.Response.Clear();
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = status;
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;

        var problemDetails = new
        {
            type = status == StatusCodes.Status409Conflict
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.10"
                : "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            title,
            status,
            detail,
            traceId = context.TraceIdentifier,
            instance = context.Request.Path.Value
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
