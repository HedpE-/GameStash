using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameStash.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFinishedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "root_folders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    path = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_root_folders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    igdb_client_id = table.Column<string>(type: "text", nullable: false),
                    igdb_secret = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.igdb_client_id);
                });

            migrationBuilder.CreateTable(
                name: "localLibrary",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PackageType = table.Column<string>(type: "text", nullable: false),
                    RootFolder = table.Column<string>(type: "text", nullable: false),
                    Folder = table.Column<string>(type: "text", nullable: false),
                    SortName = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    RootFolderId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localLibrary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_localLibrary_root_folders_RootFolderId",
                        column: x => x.RootFolderId,
                        principalTable: "root_folders",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_localLibrary_RootFolderId",
                table: "localLibrary",
                column: "RootFolderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "localLibrary");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "root_folders");
        }
    }
}
