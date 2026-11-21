using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyAndDefenseSlotConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ReportComments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RefreshTokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProgressReports",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Internships",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "InternshipOffers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                table: "Defenses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Defenses",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "DefenseJuryMembers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Conventions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Companies",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "AcademicTracks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Defenses_ScheduledAt_EndsAt",
                table: "Defenses",
                columns: new[] { "ScheduledAt", "EndsAt" });

            // Les colonnes « xmin » ci-dessus sont des colonnes système PostgreSQL : Npgsql n'émet
            // aucun SQL pour elles (elles servent uniquement de jeton de concurrence optimiste).

            // 1. Remplissage de la fin de créneau pour les soutenances existantes.
            migrationBuilder.Sql(@"
                UPDATE ""Defenses""
                SET ""EndsAt"" = ""ScheduledAt"" + make_interval(mins => ""DurationMinutes"");");

            // 2. btree_gist permet de combiner une égalité (salle) et un chevauchement d'intervalles dans un index GiST.
            //    Sur Azure Database for PostgreSQL, l'extension doit être autorisée (paramètre azure.extensions = BTREE_GIST).
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // 3. La valeur par défaut technique (-infinity) n'a servi qu'à ajouter la colonne NOT NULL.
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" ALTER COLUMN ""EndsAt"" DROP DEFAULT;");

            // 4. Invariant du domaine garanti par la base : une soutenance finit après avoir commencé.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Defenses""
                ADD CONSTRAINT ""CK_Defenses_EndsAfterStart"" CHECK (""EndsAt"" > ""ScheduledAt"");");

            // 5. Garantie au niveau base : deux soutenances ne peuvent pas occuper la même salle (insensible à la casse)
            //    sur des créneaux qui se chevauchent, même si deux requêtes concurrentes passent la vérification applicative.
            //    Intervalle semi-ouvert [début, fin[ : deux soutenances consécutives (10h-11h puis 11h-12h) restent autorisées.
            //
            //    GREATEST(EndsAt, ScheduledAt) : toute la migration s'exécute dans une seule transaction, et l'UPDATE
            //    ci-dessus laisse l'ancienne version des lignes (EndsAt = -infinity) que PostgreSQL indexe quand même
            //    lors de la construction de l'index dans la même transaction. Sans GREATEST, tstzrange lèverait
            //    « range lower bound must be less than or equal to range upper bound ». Pour les lignes réelles
            //    (EndsAt > ScheduledAt, garanti par CK_Defenses_EndsAfterStart), l'expression est identique à EndsAt.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Defenses""
                ADD CONSTRAINT ""EX_Defenses_Classroom_Slot""
                EXCLUDE USING gist (
                    lower(""Classroom"") WITH =,
                    tstzrange(""ScheduledAt"", GREATEST(""EndsAt"", ""ScheduledAt""), '[)') WITH &&
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" DROP CONSTRAINT IF EXISTS ""EX_Defenses_Classroom_Slot"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" DROP CONSTRAINT IF EXISTS ""CK_Defenses_EndsAfterStart"";");
            // L'extension btree_gist est conservée : elle peut être utilisée par d'autres objets.

            migrationBuilder.DropIndex(
                name: "IX_Defenses_ScheduledAt_EndsAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ReportComments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Internships");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "InternshipOffers");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "DefenseJuryMembers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "AcademicTracks");
        }
    }
}
