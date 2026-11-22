using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GameStash.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "amazon_stash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ExtId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortName = table.Column<string>(type: "text", nullable: false),
                    IsInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlaying = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    PlayedToEnd = table.Column<bool>(type: "boolean", nullable: false),
                    IsInPlaylist = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_amazon_stash", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "epic_stash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Uid = table.Column<string>(type: "text", nullable: true),
                    CatalogItemId = table.Column<string>(type: "text", nullable: true),
                    Namespace = table.Column<string>(type: "text", nullable: true),
                    AppName = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortName = table.Column<string>(type: "text", nullable: false),
                    IsInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlaying = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    PlayedToEnd = table.Column<bool>(type: "boolean", nullable: false),
                    IsInPlaylist = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_epic_stash", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gog_stash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ExtId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortName = table.Column<string>(type: "text", nullable: false),
                    IsInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlaying = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    PlayedToEnd = table.Column<bool>(type: "boolean", nullable: false),
                    IsInPlaylist = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gog_stash", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "local_stash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PackageType = table.Column<string>(type: "text", nullable: false),
                    RootFolder = table.Column<string>(type: "text", nullable: false),
                    Folder = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortName = table.Column<string>(type: "text", nullable: false),
                    IsInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlaying = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    PlayedToEnd = table.Column<bool>(type: "boolean", nullable: false),
                    IsInPlaylist = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_stash", x => x.Id);
                });

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
                    igdb_secret = table.Column<string>(type: "text", nullable: false),
                    steam_id64 = table.Column<string>(type: "text", nullable: false),
                    steam_api_key = table.Column<string>(type: "text", nullable: false),
                    epic_refresh_token = table.Column<string>(type: "text", nullable: false),
                    epic_access_token = table.Column<string>(type: "text", nullable: false),
                    gog_refresh_token = table.Column<string>(type: "text", nullable: false),
                    gog_access_token = table.Column<string>(type: "text", nullable: false),
                    amazon_refresh_token = table.Column<string>(type: "text", nullable: false),
                    amazon_access_token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.igdb_client_id);
                });

            migrationBuilder.CreateTable(
                name: "steam_stash",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ExtId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortName = table.Column<string>(type: "text", nullable: false),
                    IsInstalled = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayed = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlaying = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    PlayedToEnd = table.Column<bool>(type: "boolean", nullable: false),
                    IsInPlaylist = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_steam_stash", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "amazon_stash");

            migrationBuilder.DropTable(
                name: "epic_stash");

            migrationBuilder.DropTable(
                name: "gog_stash");

            migrationBuilder.DropTable(
                name: "local_stash");

            migrationBuilder.DropTable(
                name: "root_folders");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "steam_stash");
        }
    }
}
