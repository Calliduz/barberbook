using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BarberBook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentAssignmentsAndChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    equipment_id = table.Column<int>(type: "integer", nullable: false),
                    barber_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    returned_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_assignments_barbers_barber_id",
                        column: x => x.barber_id,
                        principalTable: "barbers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_equipment_assignments_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment_checks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    equipment_id = table.Column<int>(type: "integer", nullable: false),
                    checked_by_id = table.Column<int>(type: "integer", nullable: true),
                    checked_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    next_check_due = table.Column<LocalDate>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_checks", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_checks_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_equipment_checks_users_checked_by_id",
                        column: x => x.checked_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_assignments_barber_id",
                table: "equipment_assignments",
                column: "barber_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_assignments_equipment_id",
                table: "equipment_assignments",
                column: "equipment_id",
                unique: true,
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_checks_checked_by_id",
                table: "equipment_checks",
                column: "checked_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_checks_equipment_id_checked_at",
                table: "equipment_checks",
                columns: new[] { "equipment_id", "checked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_checks_next_check_due",
                table: "equipment_checks",
                column: "next_check_due");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_assignments");

            migrationBuilder.DropTable(
                name: "equipment_checks");
        }
    }
}
