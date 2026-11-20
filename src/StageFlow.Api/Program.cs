using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Serilog;
using StageFlow.Api.Middleware;
using StageFlow.Api.Services;
using StageFlow.Api.Validation;
using FluentValidation;
using StageFlow.Application;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Infrastructure;
using StageFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging structuré (console). Les sinks supplémentaires peuvent être ajoutés ici.
builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console());

// Controllers & JSON configuration
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Les énumérations sont échangées par leur nom : une valeur numérique hors enum (ex. rôle 0) est refusée.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false));
    });

// Reverse proxy (nginx, Azure Front Door…) : l'IP réelle du client n'est lue dans X-Forwarded-For
// que si la requête provient d'un proxy de confiance. Sans cela, le rate limiting verrait l'IP du proxy
// pour tous les utilisateurs, et un client direct pourrait usurper n'importe quelle IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }

    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }
});

// Rate Limiting (Protection contre le brute force sur /login)
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("refresh_limiter", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Fenêtre fixe partitionnée par adresse IP : un attaquant ne peut pas
    // verrouiller la connexion de tous les utilisateurs en épuisant le quota.
    options.AddPolicy("login_limiter", context =>
    {
        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS Configuration
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Dependencies
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddApplicationServices();
builder.Services.AddValidatorsFromAssemblyContaining<FluentValidationFilter>(includeInternalTypes: true);
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

// Health checks (API + PostgreSQL)
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
var healthChecks = builder.Services.AddHealthChecks();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!useInMemoryDatabase && !string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddNpgSql(connectionString);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StageFlow API",
        Version = "v1",
        Description = "API de gestion des stages PFE de l'ENSA : propositions, conventions, rapports, soutenances et audit."
    });

    // Bearer Token Authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Doit précéder tout middleware qui lit l'IP ou le schéma (rate limiting, HTTPS, cookies Secure).
app.UseForwardedHeaders();

// Global Exception Handler
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "StageFlow API v1"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("CorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = (int)e.Value.Duration.TotalMilliseconds
            })
        });
    }
});

// Migrations & données de démonstration : opt-in explicite, jamais activées par défaut en production.
using (var scope = app.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<StageFlowDbContextInitialiser>();
    var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
    var seedDemoData = app.Configuration.GetValue("Database:SeedDemoData", app.Environment.IsDevelopment());

    if (autoMigrate)
    {
        app.Logger.LogInformation("Application des migrations de base de données...");
        await initialiser.InitialiseAsync();
    }

    if (seedDemoData)
    {
        app.Logger.LogWarning("Mode démonstration actif : injection des comptes et données de démonstration.");
        await initialiser.SeedAsync();
    }
}

app.Run();
