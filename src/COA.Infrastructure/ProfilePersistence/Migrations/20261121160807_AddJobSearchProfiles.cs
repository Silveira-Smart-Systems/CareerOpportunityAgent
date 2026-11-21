using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace COA.Infrastructure.ProfilePersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobSearchProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobSearchProfiles",
                columns: table => new
                {
                    JobSearchProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RemoteFromCountry = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    MinimumSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: true),
                    SalaryPeriod = table.Column<byte>(type: "tinyint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchProfiles", x => x.JobSearchProfileId);
                    table.ForeignKey(
                        name: "FK_JobSearchProfiles_ProfileOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "ProfileOwners",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobSearchProfileValues",
                columns: table => new
                {
                    JobSearchProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSearchProfileValues", x => new { x.JobSearchProfileId, x.Kind, x.Value });
                    table.ForeignKey(
                        name: "FK_JobSearchProfileValues_JobSearchProfiles_JobSearchProfileId",
                        column: x => x.JobSearchProfileId,
                        principalTable: "JobSearchProfiles",
                        principalColumn: "JobSearchProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobSearchProfiles_OwnerId_IsActive",
                table: "JobSearchProfiles",
                columns: new[] { "OwnerId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSearchProfileValues");

            migrationBuilder.DropTable(
                name: "JobSearchProfiles");
        }
    }
}
