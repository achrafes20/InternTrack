using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InternTrack.Domain.Entities;
using InternTrack.Domain.Enums;
using InternTrack.Infrastructure.Identity;

namespace InternTrack.Infrastructure.Persistence;

public class InternTrackDbContextInitialiser
{
    private readonly InternTrackDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public InternTrackDbContextInitialiser(
        InternTrackDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.MigrateAsync();
        }
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedTracksAsync();
        await SeedUsersAsync();
        await SeedCompaniesAndOffersAsync();
        await SeedSampleWorkflowAsync();
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = { UserRole.Admin, UserRole.ResponsableFiliere, UserRole.Encadrant, UserRole.Etudiant };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private async Task SeedTracksAsync()
    {
        if (!await _context.AcademicTracks.AnyAsync())
        {
            var tracks = new List<AcademicTrack>
            {
                new("GI", "Génie Informatique", "Filière dédiée au génie logiciel, architectures réparties, cloud et IA"),
                new("GTR", "Génie des Télécoms et Réseaux", "Filière spécialisée dans les réseaux télécoms, 5G, cybersécurité et IoT"),
                new("GIndus", "Génie Industriel", "Logistique, supply chain, optimisation des procédés et management agile"),
                new("GE", "Génie Électrique", "Systèmes embarqués, énergies renouvelables et commande industrielle")
            };

            await _context.AcademicTracks.AddRangeAsync(tracks);
            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task SeedUsersAsync()
    {
        // Admin
        var adminEmail = "admin@ensa.ma";
        var adminUser = await _userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "ENSA",
                EmailConfirmed = true
            };

            await _userManager.CreateAsync(adminUser, "Admin@123456");
            await _userManager.AddToRoleAsync(adminUser, UserRole.Admin);
        }

        // Responsable Filière GI
        var giTrack = await _context.AcademicTracks.FirstOrDefaultAsync(t => t.Code == "GI");
        var respEmail = "responsable.gi@ensa.ma";
        var respUser = await _userManager.FindByEmailAsync(respEmail);
        if (respUser == null && giTrack != null)
        {
            respUser = new ApplicationUser
            {
                UserName = respEmail,
                Email = respEmail,
                FirstName = "Karim",
                LastName = "Alami",
                AcademicTrackId = giTrack.Id,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(respUser, "Resp@123456");
            await _userManager.AddToRoleAsync(respUser, UserRole.ResponsableFiliere);
        }

        // Encadrant
        var profEmail = "prof.tazi@ensa.ma";
        var profUser = await _userManager.FindByEmailAsync(profEmail);
        if (profUser == null)
        {
            profUser = new ApplicationUser
            {
                UserName = profEmail,
                Email = profEmail,
                FirstName = "Mohamed",
                LastName = "Tazi",
                AcademicTrackId = giTrack?.Id,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(profUser, "Prof@123456");
            await _userManager.AddToRoleAsync(profUser, UserRole.Encadrant);
        }

        // Étudiant 1 (Achraf)
        var studentEmail = "etudiant.achraf@ensa.ma";
        var studentUser = await _userManager.FindByEmailAsync(studentEmail);
        if (studentUser == null && giTrack != null)
        {
            studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                FirstName = "Achraf",
                LastName = "Bennani",
                StudentNumber = "E1452930",
                GraduationYear = 2027,
                AcademicTrackId = giTrack.Id,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(studentUser, "Student@123456");
            await _userManager.AddToRoleAsync(studentUser, UserRole.Etudiant);
        }

        // Étudiant 2 (Fatima)
        var student2Email = "etudiant.fatima@ensa.ma";
        var student2User = await _userManager.FindByEmailAsync(student2Email);
        if (student2User == null && giTrack != null)
        {
            student2User = new ApplicationUser
            {
                UserName = student2Email,
                Email = student2Email,
                FirstName = "Fatima-Zahra",
                LastName = "El Mansouri",
                StudentNumber = "E1452931",
                GraduationYear = 2027,
                AcademicTrackId = giTrack.Id,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(student2User, "Student@123456");
            await _userManager.AddToRoleAsync(student2User, UserRole.Etudiant);
        }

        // Étudiant 3 (Mehdi)
        var student3Email = "etudiant.mehdi@ensa.ma";
        var student3User = await _userManager.FindByEmailAsync(student3Email);
        if (student3User == null && giTrack != null)
        {
            student3User = new ApplicationUser
            {
                UserName = student3Email,
                Email = student3Email,
                FirstName = "Mehdi",
                LastName = "Chraibi",
                StudentNumber = "E1452932",
                GraduationYear = 2027,
                AcademicTrackId = giTrack.Id,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(student3User, "Student@123456");
            await _userManager.AddToRoleAsync(student3User, UserRole.Etudiant);
        }

        // Étudiant 4 (Yassine) : un élève ne pouvant suivre qu'un stage à la fois,
        // le dossier validé de démonstration lui appartient.
        var student4Email = "etudiant.yassine@ensa.ma";
        var student4User = await _userManager.FindByEmailAsync(student4Email);
        if (student4User == null && giTrack != null)
        {
            student4User = new ApplicationUser
            {
                UserName = student4Email,
                Email = student4Email,
                FirstName = "Yassine",
                LastName = "Amrani",
                StudentNumber = "E1452933",
                GraduationYear = 2027,
                AcademicTrackId = giTrack.Id,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(student4User, "Student@123456");
            await _userManager.AddToRoleAsync(student4User, UserRole.Etudiant);
        }
    }

    private async Task SeedCompaniesAndOffersAsync()
    {
        if (!await _context.Companies.AnyAsync())
        {
            var company1 = new Company(
                "Capgemini Morocco",
                "IT Services & Consulting",
                "Casablanca",
                "Casablanca Nearshore Park, Shore 1",
                "contact@capgemini.ma",
                "+212 522 500 000",
                "https://www.capgemini.com/ma-fr/");

            var company2 = new Company(
                "CGI Technologies",
                "Software & Digital Solutions",
                "Rabat",
                "Rabat Technopolis, Rocade Sud",
                "recrutement@cgi.com",
                "+212 537 600 000",
                "https://www.cgi.com/fr");

            var company3 = new Company(
                "OCP Digital Factory",
                "Mining & Tech Transformation",
                "Casablanca",
                "Marina Casablanca, Tour Crystal 2",
                "careers@ocpgroup.ma",
                "+212 522 230 000",
                "https://www.ocpgroup.ma");

            await _context.Companies.AddRangeAsync(company1, company2, company3);
            await _context.SaveChangesAsync(CancellationToken.None);

            var giTrack = await _context.AcademicTracks.FirstOrDefaultAsync(t => t.Code == "GI");
            if (giTrack != null)
            {
                var offer1 = new InternshipOffer(
                    "Ingénieur Logiciel .NET Core & Cloud Azure (PFE)",
                    "Conception et déploiement de microservices sous .NET 10, EF Core et conteneurs Docker sur Azure AKS.",
                    "C#, ASP.NET Core, EF Core, PostgreSQL, Docker, Azure",
                    "Casablanca",
                    24,
                    5000m,
                    DateTime.UtcNow.AddMonths(3),
                    company1.Id,
                    giTrack.Id);

                var offer2 = new InternshipOffer(
                    "Développeur Full Stack React / .NET (Stage 5A)",
                    "Participation au développement d'une plateforme d'entreprise réactive avec React 19, TypeScript et Clean Architecture.",
                    "React, TypeScript, C#, Web API, Tailwind / UI5",
                    "Rabat",
                    20,
                    4500m,
                    DateTime.UtcNow.AddMonths(2),
                    company2.Id,
                    giTrack.Id);

                var offer3 = new InternshipOffer(
                    "Data Engineer & Pipeline Streaming (PFE)",
                    "Développement de pipelines Kafka et architectures analytiques distribuées pour l'industrie 4.0.",
                    "Python, Apache Kafka, Spark, Databricks, PostgreSQL",
                    "Casablanca",
                    24,
                    5500m,
                    DateTime.UtcNow.AddMonths(4),
                    company3.Id,
                    giTrack.Id);

                await _context.InternshipOffers.AddRangeAsync(offer1, offer2, offer3);
                await _context.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private async Task SeedSampleWorkflowAsync()
    {
        var studentAchraf = await _userManager.FindByEmailAsync("etudiant.achraf@ensa.ma");
        var studentFatima = await _userManager.FindByEmailAsync("etudiant.fatima@ensa.ma");
        var studentMehdi = await _userManager.FindByEmailAsync("etudiant.mehdi@ensa.ma");
        var studentYassine = await _userManager.FindByEmailAsync("etudiant.yassine@ensa.ma");
        var profTazi = await _userManager.FindByEmailAsync("prof.tazi@ensa.ma");
        var respAlami = await _userManager.FindByEmailAsync("responsable.gi@ensa.ma");

        var giTrack = await _context.AcademicTracks.FirstOrDefaultAsync(t => t.Code == "GI");
        var capgemini = await _context.Companies.FirstOrDefaultAsync(c => c.Name.Contains("Capgemini"));
        var cgi = await _context.Companies.FirstOrDefaultAsync(c => c.Name.Contains("CGI"));
        var ocp = await _context.Companies.FirstOrDefaultAsync(c => c.Name.Contains("OCP"));

        if (studentAchraf != null && giTrack != null && capgemini != null && profTazi != null && respAlami != null)
        {
            // 1. Stage EN COURS pour Achraf (Permet d'ajouter des rapports, d'être planifié en soutenance)
            var hasActiveAchraf = await _context.Internships.AnyAsync(i => i.StudentId == studentAchraf.Id && i.Status == InternshipStatus.EnCours);
            if (!hasActiveAchraf)
            {
                var stageAchraf = new Internship(
                    "Architecture Microservices & Observabilité Cloud (PFE)",
                    "Implémentation d'une infrastructure hautement disponible avec .NET 10, Docker Compose, monitoring Prometheus/Grafana et CI/CD GitHub Actions.",
                    DateTime.UtcNow.AddMonths(-2),
                    DateTime.UtcNow.AddMonths(4),
                    studentAchraf.Id,
                    capgemini.Id,
                    giTrack.Id,
                    "Mehdi Benkirane",
                    "m.benkirane@capgemini.ma",
                    "+212 661 223344"
                );

                // Workflow : Brouillon -> Propose -> Valide -> EnCours
                stageAchraf.SubmitForApproval(studentAchraf.Id);
                stageAchraf.Approve(respAlami.Id);
                stageAchraf.StartInternship(respAlami.Id);
                stageAchraf.AssignAcademicSupervisor(profTazi.Id);

                await _context.Internships.AddAsync(stageAchraf);
                await _context.SaveChangesAsync(CancellationToken.None);

                // Convention officielle
                var convention = Convention.CreateFor(stageAchraf.Id, DateTime.UtcNow);
                convention.SignByStudent();
                convention.SignByCompany();
                convention.SignBySchool();
                await _context.Conventions.AddAsync(convention);

                // Rapports de suivi pour Achraf
                var report1 = new ProgressReport(
                    stageAchraf.Id,
                    1,
                    "Rapport Semaine 1 : Immersion et cadrage d'architecture",
                    "Prise en main de l'environnement, intégration dans l'équipe DevOps, analyse du cahier des charges et des contraintes techniques.",
                    "Docker, Clean Architecture, Git flow, PostgreSQL",
                    "Configuration initiale des accès VPN d'entreprise.",
                    "Mise en place de la stack d'authentification JWT et Cookie HttpOnly."
                );
                report1.MarkAsValidated();

                var comment1 = new ReportComment(
                    report1.Id,
                    profTazi.Id,
                    "Mohamed Tazi (Encadrant)",
                    "Excellent démarrage Achraf. Veille bien à isoler les couches domaine et infrastructure comme vu en cours."
                );

                var report2 = new ProgressReport(
                    stageAchraf.Id,
                    2,
                    "Rapport Semaine 2 : Implémentation du moteur de règles et ledger immuable",
                    "Développement des aggregates du domaine, configuration du trigger append-only sur PostgreSQL et tests d'intégration xUnit.",
                    "EF Core Interceptors, Triggers PostgreSQL, xUnit, FluentAssertions",
                    "Gestion fine de la désérialisation JSON des DTOs dans ASP.NET Core.",
                    "Finalisation du composant frontend sous React 19 et SAP UI5."
                );

                await _context.ProgressReports.AddRangeAsync(report1, report2);
                await _context.ReportComments.AddAsync(comment1);
            }

            // 2. Stage VALIDÉ en attente de démarrage (pour tester le bouton "Démarrer le stage" par le Responsable)
            var hasValideStage = await _context.Internships.AnyAsync(i => i.Status == InternshipStatus.Valide);
            if (!hasValideStage && ocp != null && studentYassine != null)
            {
                // Stage validé qui commence la semaine prochaine (signature entreprise encore attendue).
                var stageValide = new Internship(
                    "Data Pipeline Temps Réel & Analyse Prédictive",
                    "Conception d'une architecture streaming avec Apache Kafka et intégration d'un modèle d'IA pour la détection précoce d'anomalies sur les chaînes de traitement.",
                    DateTime.UtcNow.Date.AddDays(7),
                    DateTime.UtcNow.Date.AddDays(7).AddMonths(6),
                    studentYassine.Id,
                    ocp.Id,
                    giTrack.Id,
                    "Anas Tazi",
                    "a.tazi@ocpgroup.ma",
                    "+212 663 889900"
                );
                stageValide.SubmitForApproval(studentYassine.Id);
                stageValide.Approve(respAlami.Id);
                stageValide.AssignAcademicSupervisor(profTazi.Id);

                await _context.Internships.AddAsync(stageValide);
                await _context.SaveChangesAsync(CancellationToken.None);

                var conv2 = Convention.CreateFor(stageValide.Id, DateTime.UtcNow);
                conv2.SignByStudent();
                conv2.SignBySchool();
                await _context.Conventions.AddAsync(conv2);
            }

            // 3. Stage PROPOSÉ pour Fatima en attente de validation (pour tester "Valider" ou "Refuser")
            if (studentFatima != null && cgi != null)
            {
                var hasProposeStage = await _context.Internships.AnyAsync(i => i.Status == InternshipStatus.Propose);
                if (!hasProposeStage)
                {
                    var stageFatima = new Internship(
                        "Plateforme Mobile Flutter & Analyse Prédictive",
                        "Conception et développement d'une application cross-platform pour le suivi en temps réel des flux de production avec modèles de détection d'anomalies.",
                        DateTime.UtcNow.AddMonths(-1),
                        DateTime.UtcNow.AddMonths(5),
                        studentFatima.Id,
                        cgi.Id,
                        giTrack.Id,
                        "Salma Berrada",
                        "s.berrada@cgi.com",
                        "+212 662 556677"
                    );
                    stageFatima.SubmitForApproval(studentFatima.Id);
                    await _context.Internships.AddAsync(stageFatima);
                }
            }

            // 4. Stage avec SOUTENANCE PLANIFIÉE (pour visualiser le calendrier des soutenances et KPIs du dashboard)
            var hasScheduledDefense = await _context.Defenses.AnyAsync();
            if (!hasScheduledDefense && studentMehdi != null && cgi != null)
            {
                var stageMehdi = new Internship(
                    "Système Embarqué & Vision par Ordinateur pour Véhicules Autonomes",
                    "Implémentation d'algorithmes de détection d'obstacles en temps réel sur microcontrôleurs embarqués avec edge inference.",
                    DateTime.UtcNow.AddMonths(-3),
                    DateTime.UtcNow.AddMonths(3),
                    studentMehdi.Id,
                    cgi.Id,
                    giTrack.Id,
                    "Hassan Bennani",
                    "h.bennani@cgi.com",
                    "+212 664 112233"
                );
                stageMehdi.SubmitForApproval(studentMehdi.Id);
                stageMehdi.Approve(respAlami.Id);
                stageMehdi.StartInternship(respAlami.Id);
                stageMehdi.AssignAcademicSupervisor(profTazi.Id);

                await _context.Internships.AddAsync(stageMehdi);
                await _context.SaveChangesAsync(CancellationToken.None);

                var defenseDate = DateTime.UtcNow.AddDays(5).Date.AddHours(14).AddMinutes(30);
                var defense = new Defense(stageMehdi.Id, defenseDate, "Salle des Thèses B204", 45);
                var jury1 = new DefenseJuryMember(defense.Id, respAlami.Id, "Karim Alami", DefenseRole.President);
                var jury2 = new DefenseJuryMember(defense.Id, profTazi.Id, "Mohamed Tazi", DefenseRole.Examinateur);

                await _context.Defenses.AddAsync(defense);
                await _context.DefenseJuryMembers.AddRangeAsync(jury1, jury2);
                stageMehdi.MarkDefenseScheduled(respAlami.Id);

                var convMehdi = Convention.CreateFor(stageMehdi.Id, DateTime.UtcNow);
                convMehdi.SignByStudent();
                convMehdi.SignByCompany();
                convMehdi.SignBySchool();
                await _context.Conventions.AddAsync(convMehdi);
            }

            await _context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
