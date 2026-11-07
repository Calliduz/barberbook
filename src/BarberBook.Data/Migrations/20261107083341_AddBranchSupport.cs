using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BarberBook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "branch_id",
                table: "barbers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "branch_id",
                table: "appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    opening_time = table.Column<LocalTime>(type: "time", nullable: false),
                    closing_time = table.Column<LocalTime>(type: "time", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_barbers_branch_id",
                table: "barbers",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_branch_id",
                table: "appointments",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_name",
                table: "branches",
                column: "name");

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_branches_branch_id",
                table: "appointments",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_barbers_branches_branch_id",
                table: "barbers",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_appointments_branches_branch_id",
                table: "appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_barbers_branches_branch_id",
                table: "barbers");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropIndex(
                name: "IX_barbers_branch_id",
                table: "barbers");

            migrationBuilder.DropIndex(
                name: "IX_appointments_branch_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "barbers");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "appointments");
        }
    }
}
