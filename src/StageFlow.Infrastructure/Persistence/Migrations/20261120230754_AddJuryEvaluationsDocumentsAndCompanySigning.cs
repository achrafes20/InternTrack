using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StageFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJuryEvaluationsDocumentsAndCompanySigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GradeCorrectedAt",
                table: "Defenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradeCorrectionReason",
                table: "Defenses",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradedAt",
                table: "Defenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradedBy",
                table: "Defenses",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompanySignedAt",
                table: "Conventions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanySignerName",
                table: "Conventions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompanySigningTokenExpiresAt",
                table: "Conventions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanySigningTokenHash",
                table: "Conventions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DefenseEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TeacherFullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Grade = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefenseEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefenseEvaluations_Defenses_DefenseId",
                        column: x => x.DefenseId,
                        principalTable: "Defenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InternshipDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InternshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternshipDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternshipDocuments_Internships_InternshipId",
                        column: x => x.InternshipId,
                        principalTable: "Internships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conventions_CompanySigningTokenHash",
                table: "Conventions",
                column: "CompanySigningTokenHash",
                unique: true,
                filter: "\"CompanySigningTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DefenseEvaluations_DefenseId_TeacherId",
                table: "DefenseEvaluations",
                columns: new[] { "DefenseId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternshipDocuments_InternshipId_Kind",
                table: "InternshipDocuments",
                columns: new[] { "InternshipId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_InternshipDocuments_StorageKey",
                table: "InternshipDocuments",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefenseEvaluations");

            migrationBuilder.DropTable(
                name: "InternshipDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Conventions_CompanySigningTokenHash",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "GradeCorrectedAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "GradeCorrectionReason",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "GradedAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "GradedBy",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "CompanySignedAt",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "CompanySignerName",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "CompanySigningTokenExpiresAt",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "CompanySigningTokenHash",
                table: "Conventions");
        }
    }
}
