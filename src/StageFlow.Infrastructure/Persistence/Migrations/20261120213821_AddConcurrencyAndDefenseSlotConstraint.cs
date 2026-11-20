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

            // 3. Garantie au niveau base : deux soutenances ne peuvent pas occuper la même salle (insensible à la casse)
            //    sur des créneaux qui se chevauchent, même si deux requêtes concurrentes passent la vérification applicative.
            //    Intervalle semi-ouvert [début, fin[ : deux soutenances consécutives (10h-11h puis 11h-12h) restent autorisées.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Defenses""
                ADD CONSTRAINT ""EX_Defenses_Classroom_Slot""
                EXCLUDE USING gist (
                    lower(""Classroom"") WITH =,
                    tstzrange(""ScheduledAt"", ""EndsAt"", '[)') WITH &&
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" DROP CONSTRAINT IF EXISTS ""EX_Defenses_Classroom_Slot"";");
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
