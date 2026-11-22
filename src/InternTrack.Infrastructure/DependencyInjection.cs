using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using InternTrack.Application.Common.Interfaces;
using InternTrack.Domain.Enums;
using InternTrack.Infrastructure.Identity;
using InternTrack.Infrastructure.Persistence;
using InternTrack.Infrastructure.Persistence.Interceptors;
using InternTrack.Infrastructure.Services.Ai;
using InternTrack.Infrastructure.Services.Antivirus;
using InternTrack.Infrastructure.Services.Background;
using InternTrack.Infrastructure.Services.Email;
using InternTrack.Infrastructure.Services.Pdf;
using InternTrack.Infrastructure.Services.Qr;
using InternTrack.Infrastructure.Services.Storage;
using InternTrack.Infrastructure.Services.Time;
using InternTrack.Application.Features.Internships;

namespace InternTrack.Infrastructure;

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

        services.AddDbContext<InternTrackDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options.UseInMemoryDatabase(configuration["InMemoryDatabaseName"] ?? "InternTrackInMemoryDb");
            }
            else
            {
                options.UseNpgsql(connectionString ?? "Host=localhost;Port=5432;Database=interntrack_db;Username=postgres;Password=postgres", 
                    b => b.MigrationsAssembly(typeof(InternTrackDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<InternTrackDbContext>());
        services.AddScoped<InternTrackDbContextInitialiser>();

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
        .AddEntityFrameworkStores<InternTrackDbContext>()
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
        var dataProtection = services.AddDataProtection().SetApplicationName("InternTrack");
        var useAzureBlob = string.Equals(configuration["Storage:Provider"], "AzureBlob", StringComparison.OrdinalIgnoreCase);
        var keysPath = configuration["DataProtection:KeysPath"];
        if (useAzureBlob)
        {
            // Plusieurs instances : le trousseau de clés doit être partagé, il est donc stocké dans Azure Blob.
            var container = AzureBlobFileStorage.CreateContainerClient(configuration);
            services.AddSingleton(container);
            services.AddHostedService<AzureBlobContainerInitializer>();
            dataProtection.PersistKeysToAzureBlobStorage(container.GetBlobClient("dataprotection/keys.xml"));
        }
        else if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        // Fichiers déposés : disque local (volume Docker) ou Azure Blob Storage (Storage:Provider=AzureBlob).
        if (useAzureBlob)
            services.AddSingleton<IFileStorage>(sp => new AzureBlobFileStorage(sp.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>()));
        else
            services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Antivirus des fichiers déposés : ClamAV si Antivirus:Enabled=true (refus si injoignable).
        if (configuration.GetValue<bool>("Antivirus:Enabled"))
            services.AddSingleton<IMalwareScanner, ClamAvMalwareScanner>();
        else
            services.AddSingleton<IMalwareScanner, NoOpMalwareScanner>();
        services.AddSingleton(InternshipPolicy.FromConfiguration(configuration));
        services.AddSingleton(InternTrack.Application.Features.Defenses.DefensePolicy.FromConfiguration(configuration));

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPdfGenerator, QuestPdfGenerator>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddHttpClient<IAiSummaryService, GeminiAiSummaryService>();

        // E-mails : outbox transactionnelle (IEmailSender) + envoi en arrière-plan avec nouveaux essais.
        // Transport SMTP si configuré, sinon simple journalisation (développement).
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<EmailOutboxSignal>();
        services.AddScoped<IEmailSender, OutboxEmailSender>();
        services.AddScoped<IEmailOutboxMonitor, EmailOutboxMonitor>();
        if (configuration.GetValue<bool>("Smtp:Enabled"))
        {
            services.AddScoped<IEmailTransport, SmtpEmailTransport>();
        }
        else
        {
            services.AddScoped<IEmailTransport, LoggingEmailTransport>();
        }
        services.AddHostedService<EmailOutboxProcessor>();

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
