using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace COA.Infrastructure.ProfilePersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyntheticOpportunityProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSynthetic",
                table: "Opportunities",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSynthetic",
                table: "Opportunities");
        }
    }
}
