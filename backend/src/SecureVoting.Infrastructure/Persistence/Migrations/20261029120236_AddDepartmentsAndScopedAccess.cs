using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureVoting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsAndScopedAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Students",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Elections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodeNormalized = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Faculty = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "Departments"
                    ("Id", "Name", "Code", "CodeNormalized", "Faculty", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    gen_random_uuid(),
                    d."Name",
                    'LEGACY-' || upper(substr(md5(lower(d."Name")), 1, 8)),
                    'LEGACY-' || upper(substr(md5(lower(d."Name")), 1, 8)),
                    NULL,
                    TRUE,
                    now(),
                    now()
                FROM (
                    SELECT DISTINCT ON (lower(btrim(source."Department")))
                        btrim(source."Department") AS "Name"
                    FROM (
                        SELECT "Department" FROM "Students"
                        UNION ALL
                        SELECT "Department" FROM "Elections"
                    ) AS source
                    WHERE length(btrim(source."Department")) > 0
                    ORDER BY lower(btrim(source."Department")), btrim(source."Department")
                ) AS d;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Departments"
                    ("Id", "Name", "Code", "CodeNormalized", "Faculty", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT gen_random_uuid(), 'Unassigned (legacy)', 'UNASSIGNED', 'UNASSIGNED', NULL, TRUE, now(), now()
                WHERE EXISTS (
                    SELECT 1 FROM "Students" WHERE length(btrim("Department")) = 0
                    UNION ALL
                    SELECT 1 FROM "Elections" WHERE length(btrim("Department")) = 0
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Students" AS s
                SET "DepartmentId" = d."Id"
                FROM "Departments" AS d
                WHERE s."DepartmentId" IS NULL
                  AND lower(btrim(s."Department")) = lower(d."Name");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Elections" AS e
                SET "DepartmentId" = d."Id"
                FROM "Departments" AS d
                WHERE e."DepartmentId" IS NULL
                  AND lower(btrim(e."Department")) = lower(d."Name");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Students"
                SET "DepartmentId" = (SELECT "Id" FROM "Departments" WHERE "CodeNormalized" = 'UNASSIGNED' LIMIT 1)
                WHERE "DepartmentId" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Elections"
                SET "DepartmentId" = (SELECT "Id" FROM "Departments" WHERE "CodeNormalized" = 'UNASSIGNED' LIMIT 1)
                WHERE "DepartmentId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Students",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                table: "Elections",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "DepartmentAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentAssignments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentAssignments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE "AuditLogs" AS a
                SET "DepartmentId" = e."DepartmentId"
                FROM "Elections" AS e
                WHERE a."DepartmentId" IS NULL
                  AND a."EntityType" = 'Election'
                  AND a."EntityId" = e."Id"::text;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Students_DepartmentId",
                table: "Students",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Elections_DepartmentId",
                table: "Elections",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_DepartmentId",
                table: "AuditLogs",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAssignments_CreatedByUserId",
                table: "DepartmentAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAssignments_DepartmentId",
                table: "DepartmentAssignments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAssignments_UserId_DepartmentId_Scope",
                table: "DepartmentAssignments",
                columns: new[] { "UserId", "DepartmentId", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CodeNormalized",
                table: "Departments",
                column: "CodeNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsActive",
                table: "Departments",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Departments_DepartmentId",
                table: "AuditLogs",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Elections_Departments_DepartmentId",
                table: "Elections",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Departments_DepartmentId",
                table: "Students",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Departments_DepartmentId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Elections_Departments_DepartmentId",
                table: "Elections");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Departments_DepartmentId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "DepartmentAssignments");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Students_DepartmentId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Elections_DepartmentId",
                table: "Elections");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_DepartmentId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Elections");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "AuditLogs");
        }
    }
}
