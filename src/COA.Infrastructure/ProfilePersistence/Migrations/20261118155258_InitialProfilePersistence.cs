using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace COA.Infrastructure.ProfilePersistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProfilePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfileOwners",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerKey = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Subject = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileOwners", x => x.OwnerId);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.ProfileId);
                    table.CheckConstraint("CK_Profiles_CurrentVersion", "[CurrentVersion] >= 1");
                    table.ForeignKey(
                        name: "FK_Profiles_ProfileOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "ProfileOwners",
                        principalColumn: "OwnerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileVersions",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreferredArrangement = table.Column<byte>(type: "tinyint", nullable: false),
                    RemoteOnly = table.Column<bool>(type: "bit", nullable: false),
                    MinimumHourlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: true),
                    MaximumOfficeDaysPerWeek = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileVersions", x => new { x.ProfileId, x.Version });
                    table.CheckConstraint("CK_ProfileVersions_CurrencyLength", "[Currency] IS NULL OR LEN([Currency]) = 3");
                    table.CheckConstraint("CK_ProfileVersions_MaximumOfficeDays", "[MaximumOfficeDaysPerWeek] IS NULL OR [MaximumOfficeDaysPerWeek] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_ProfileVersions_MinimumHourlyRate", "[MinimumHourlyRate] IS NULL OR ([MinimumHourlyRate] >= 0 AND [MinimumHourlyRate] <= 100000)");
                    table.CheckConstraint("CK_ProfileVersions_PreferredArrangement", "[PreferredArrangement] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_ProfileVersions_RateCurrency", "[MinimumHourlyRate] IS NULL OR [Currency] IS NOT NULL");
                    table.CheckConstraint("CK_ProfileVersions_RemoteOnly", "[RemoteOnly] = 0 OR ([PreferredArrangement] = 0 AND ([MaximumOfficeDaysPerWeek] IS NULL OR [MaximumOfficeDaysPerWeek] = 0))");
                    table.CheckConstraint("CK_ProfileVersions_Version", "[Version] >= 1");
                    table.ForeignKey(
                        name: "FK_ProfileVersions_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileEvidence",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Claim = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origin = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileEvidence", x => new { x.ProfileId, x.Version, x.Ordinal });
                    table.CheckConstraint("CK_ProfileEvidence_Ordinal", "[Ordinal] BETWEEN 0 AND 199");
                    table.CheckConstraint("CK_ProfileEvidence_Origin", "[Origin] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_ProfileEvidence_ProfileVersions_ProfileId_Version",
                        columns: x => new { x.ProfileId, x.Version },
                        principalTable: "ProfileVersions",
                        principalColumns: new[] { "ProfileId", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ProfileOwners_OwnerKey",
                table: "ProfileOwners",
                column: "OwnerKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Profiles_OwnerId",
                table: "Profiles",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileEvidence");

            migrationBuilder.DropTable(
                name: "ProfileVersions");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "ProfileOwners");
        }
    }
}
