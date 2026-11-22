using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearCancellationTutorEvaluationAndSignedSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "Internships",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Internships",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TutorEvaluationTokenExpiresAt",
                table: "Defenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TutorEvaluationTokenHash",
                table: "Defenses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedDocumentSha256",
                table: "Conventions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedDocumentStorageKey",
                table: "Conventions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GraduationYear",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            // Année universitaire des stages existants : l'année commence le 1er septembre
            // (même règle que Internship.ComputeAcademicYear).
            migrationBuilder.Sql(@"
                UPDATE ""Internships""
                SET ""AcademicYear"" =
                    CASE WHEN EXTRACT(MONTH FROM ""StartDate"") >= 9
                         THEN EXTRACT(YEAR FROM ""StartDate"")::int || '-' || (EXTRACT(YEAR FROM ""StartDate"")::int + 1)
                         ELSE (EXTRACT(YEAR FROM ""StartDate"")::int - 1) || '-' || EXTRACT(YEAR FROM ""StartDate"")::int
                    END;");

            migrationBuilder.Sql(@"ALTER TABLE ""Internships"" ALTER COLUMN ""AcademicYear"" DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_Internships_AcademicYear",
                table: "Internships",
                column: "AcademicYear");

            migrationBuilder.CreateIndex(
                name: "IX_Defenses_TutorEvaluationTokenHash",
                table: "Defenses",
                column: "TutorEvaluationTokenHash",
                unique: true,
                filter: "\"TutorEvaluationTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Internships_AcademicYear",
                table: "Internships");

            migrationBuilder.DropIndex(
                name: "IX_Defenses_TutorEvaluationTokenHash",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "Internships");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Internships");

            migrationBuilder.DropColumn(
                name: "TutorEvaluationTokenExpiresAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "TutorEvaluationTokenHash",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "SignedDocumentSha256",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "SignedDocumentStorageKey",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "GraduationYear",
                table: "AspNetUsers");
        }
    }
}
