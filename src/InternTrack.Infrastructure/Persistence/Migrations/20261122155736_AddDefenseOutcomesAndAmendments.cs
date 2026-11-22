using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefenseOutcomesAndAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "Defenses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Defenses",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Defenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousAttemptGrade",
                table: "Defenses",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousAttemptGradedAt",
                table: "Defenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminatedAt",
                table: "Conventions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "Conventions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConventionAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConventionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    NewStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewTitle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    NewSubjectDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NewCompanySupervisorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NewCompanySupervisorEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NewCompanySupervisorPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ChangesSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsSignedByStudent = table.Column<bool>(type: "boolean", nullable: false),
                    IsSignedByCompany = table.Column<bool>(type: "boolean", nullable: false),
                    IsSignedBySchool = table.Column<bool>(type: "boolean", nullable: false),
                    CompanySignerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConventionAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConventionAmendments_Conventions_ConventionId",
                        column: x => x.ConventionId,
                        principalTable: "Conventions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConventionAmendments_ConventionId_Number",
                table: "ConventionAmendments",
                columns: new[] { "ConventionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConventionAmendments_SinglePending",
                table: "ConventionAmendments",
                column: "ConventionId",
                unique: true,
                filter: "\"AppliedAt\" IS NULL AND \"WithdrawnAt\" IS NULL");

            // Une soutenance annulée (reportée) libère sa salle : la contrainte d'exclusion ne porte plus
            // que sur les soutenances actives.
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" DROP CONSTRAINT IF EXISTS ""EX_Defenses_Classroom_Slot"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Defenses""
                ADD CONSTRAINT ""EX_Defenses_Classroom_Slot""
                EXCLUDE USING gist (
                    lower(""Classroom"") WITH =,
                    tstzrange(""ScheduledAt"", GREATEST(""EndsAt"", ""ScheduledAt""), '[)') WITH &&
                ) WHERE (""CancelledAt"" IS NULL);");

            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" ADD CONSTRAINT ""CK_Defenses_Attempt"" CHECK (""Attempt"" >= 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" DROP CONSTRAINT IF EXISTS ""CK_Defenses_Attempt"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Defenses"" DROP CONSTRAINT IF EXISTS ""EX_Defenses_Classroom_Slot"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Defenses""
                ADD CONSTRAINT ""EX_Defenses_Classroom_Slot""
                EXCLUDE USING gist (
                    lower(""Classroom"") WITH =,
                    tstzrange(""ScheduledAt"", GREATEST(""EndsAt"", ""ScheduledAt""), '[)') WITH &&
                );");

            migrationBuilder.DropTable(
                name: "ConventionAmendments");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "PreviousAttemptGrade",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "PreviousAttemptGradedAt",
                table: "Defenses");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "Conventions");
        }
    }
}
