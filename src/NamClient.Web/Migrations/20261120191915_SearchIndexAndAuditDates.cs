using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamClient.Web.Migrations
{
    /// <inheritdoc />
    public partial class SearchIndexAndAuditDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_Name",
                table: "Clients");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Contacts",
                type: "datetime2(0)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Contacts",
                type: "datetime2(0)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Clients",
                type: "datetime2(0)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Clients",
                type: "datetime2(0)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateTable(
                name: "ClientNameWords",
                columns: table => new
                {
                    Word = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientNameWords", x => new { x.Word, x.ClientId });
                    table.ForeignKey(
                        name: "FK_ClientNameWords_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Name",
                table: "Contacts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name")
                .Annotation("SqlServer:Include", new[] { "ClientCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientNameWords_ClientId",
                table: "ClientNameWords",
                column: "ClientId");

            // Backfill search words for clients that existed before this migration: punctuation
            // becomes spaces, then each name is split into lower-case words. New and renamed
            // clients get their words from the application (SearchText.Words).
            migrationBuilder.Sql("""
                INSERT INTO dbo.ClientNameWords (Word, ClientId)
                SELECT DISTINCT LEFT(LOWER(words.value), 100), c.ClientId
                FROM dbo.Clients AS c
                CROSS APPLY STRING_SPLIT(TRANSLATE(c.Name, '.,&-/()''"!?:;+@#_*', '                  '), ' ') AS words
                WHERE words.value <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientNameWords");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_Name",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Clients_Name",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");
        }
    }
}
