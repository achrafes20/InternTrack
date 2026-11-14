using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StageFlow.Application.Common.Interfaces;
using StageFlow.Domain.Entities;
using StageFlow.Infrastructure.Identity;
using StageFlow.Infrastructure.Persistence.Interceptors;

namespace StageFlow.Infrastructure.Persistence;

public class StageFlowDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly AuditableEntityInterceptor? _auditableEntityInterceptor;
    private readonly AuditLedgerInterceptor? _auditLedgerInterceptor;

    public StageFlowDbContext(
        DbContextOptions<StageFlowDbContext> options,
        AuditableEntityInterceptor? auditableEntityInterceptor = null,
        AuditLedgerInterceptor? auditLedgerInterceptor = null) : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
        _auditLedgerInterceptor = auditLedgerInterceptor;
    }

    public DbSet<AcademicTrack> AcademicTracks => Set<AcademicTrack>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<InternshipOffer> InternshipOffers => Set<InternshipOffer>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<Convention> Conventions => Set<Convention>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<ReportComment> ReportComments => Set<ReportComment>();
    public DbSet<Defense> Defenses => Set<Defense>();
    public DbSet<DefenseJuryMember> DefenseJuryMembers => Set<DefenseJuryMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_auditableEntityInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        }

        if (_auditLedgerInterceptor != null)
        {
            optionsBuilder.AddInterceptors(_auditLedgerInterceptor);
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

