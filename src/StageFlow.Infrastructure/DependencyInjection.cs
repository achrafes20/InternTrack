using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Enums;
using StageFlow.Infrastructure.Identity;
using StageFlow.Infrastructure.Persistence;
using StageFlow.Infrastructure.Persistence.Interceptors;
using StageFlow.Infrastructure.Services.Ai;
using StageFlow.Infrastructure.Services.Background;
using StageFlow.Infrastructure.Services.Email;
using StageFlow.Infrastructure.Services.Pdf;
using StageFlow.Infrastructure.Services.Qr;
using StageFlow.Infrastructure.Services.Storage;
using StageFlow.Infrastructure.Services.Time;
using StageFlow.Application.Features.Internships;

namespace StageFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditLedgerInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<StageFlowDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options.UseInMemoryDatabase(configuration["InMemoryDatabaseName"] ?? "StageFlowInMemoryDb");
            }
            else
            {
                options.UseNpgsql(connectionString ?? "Host=localhost;Port=5432;Database=stageflow_db;Username=postgres;Password=postgres", 
                    b => b.MigrationsAssembly(typeof(StageFlowDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<StageFlowDbContext>());
        services.AddScoped<StageFlowDbContextInitialiser>();

        // Identity Configuration (PBKDF2 Password Hashing default)
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;

            // Verrouillage du compte après 5 échecs consécutifs, pendant 15 minutes.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = configuration.GetValue("Security:MaxFailedLoginAttempts", 5);
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(configuration.GetValue("Security:LockoutMinutes", 15));
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<StageFlowDbContext>()
        .AddDefaultTokenProviders();

        // JWT Settings & Service
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);
        var jwtSettings = jwtSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException("La section de configuration 'JwtSettings' est absente.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
        {
            throw new InvalidOperationException(
                "JwtSettings:Secret est manquant ou trop court (32 caractères minimum). " +
                "Configurez-le via appsettings.Development.json, une variable d'environnement " +
                "(JwtSettings__Secret) ou les user-secrets. Ne jamais committer de secret de production.");
        }

        // Liens de réinitialisation de mot de passe valables 2 heures.
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

        // Clés de protection des données (jetons de réinitialisation…) persistées hors du conteneur :
        // sans cela, chaque redémarrage invaliderait les liens envoyés par e-mail.
        var dataProtection = services.AddDataProtection().SetApplicationName("StageFlow");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton(InternshipPolicy.FromConfiguration(configuration));

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPdfGenerator, QuestPdfGenerator>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddHttpClient<IAiSummaryService, GeminiAiSummaryService>();

        // Email Service (Fake in Dev/Default, Smtp if configured)
        if (configuration.GetValue<bool>("Smtp:Enabled"))
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, FakeEmailSender>();
        }

        // Background Service
        services.AddHostedService<NotificationBackgroundService>();

        // Authentication & Policies
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdminRole", policy => policy.RequireRole(UserRole.Admin));
            options.AddPolicy("RequireDepartmentHeadRole", policy => policy.RequireRole(UserRole.ResponsableFiliere, UserRole.Admin));
            options.AddPolicy("RequireSupervisorRole", policy => policy.RequireRole(UserRole.Encadrant, UserRole.ResponsableFiliere, UserRole.Admin));
            options.AddPolicy("RequireStudentRole", policy => policy.RequireRole(UserRole.Etudiant));
        });

        return services;
    }
}
