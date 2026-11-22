using GameStash.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameStash.Core.Data
{
    public class GameStashDbContext : DbContext
    {
        public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=gamestash;Username=postgres;Password=postgres";

        private static string _connectionString = DefaultConnectionString;

        public DbSet<LocalStashGame> LocalStash { get; set; }
        public DbSet<SteamStashGame> SteamStash { get; set; }
        public DbSet<EpicStashGame> EpicStash { get; set; }
        public DbSet<GogStashGame> GogStash { get; set; }
        public DbSet<AmazonStashGame> AmazonStash { get; set; }
        public DbSet<Settings> Settings { get; set; }
        public DbSet<RootFolder> RootFolders { get; set; }

        public static void ConfigureConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));

            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseNpgsql(_connectionString);

            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)
            );
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // -------------------------------------------------------------
            // 1. Settings Table Mapping
            // -------------------------------------------------------------
            modelBuilder.Entity<Settings>(entity =>
            {
                entity.ToTable("settings");
                entity.HasKey(s => s.IGDB_Client_ID);
                entity.Property(s => s.IGDB_Client_ID).HasColumnName("igdb_client_id").IsRequired();
                entity.Property(s => s.IGDB_Secret).HasColumnName("igdb_secret").IsRequired();
                entity.Property(s => s.SteamId64).HasColumnName("steam_id64").IsRequired();
                entity.Property(s => s.SteamApiKey).HasColumnName("steam_api_key").IsRequired();
                entity.Property(s => s.EpicRefreshToken).HasColumnName("epic_refresh_token").IsRequired();
                entity.Property(s => s.EpicAccessToken).HasColumnName("epic_access_token").IsRequired();
                entity.Property(s => s.GogRefreshToken).HasColumnName("gog_refresh_token").IsRequired();
                entity.Property(s => s.GogAccessToken).HasColumnName("gog_access_token").IsRequired();
                entity.Property(s => s.AmazonRefreshToken).HasColumnName("amazon_refresh_token").IsRequired();
                entity.Property(s => s.AmazonAccessToken).HasColumnName("amazon_access_token").IsRequired();
            });

            // -------------------------------------------------------------
            // 2. Root Folders Table Mapping
            // -------------------------------------------------------------
            modelBuilder.Entity<RootFolder>(entity =>
            {
                entity.ToTable("root_folders");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(r => r.Path).HasColumnName("path").IsRequired();
                entity.Ignore(s => s.UsedSpace);
                entity.Ignore(s => s.FreeSpace);
                entity.Ignore(r => r.FoundGames);
                entity.Ignore(s => s.FoundGamesCount);
                entity.Ignore(s => s.IsImportingFolders);
                entity.Ignore(s => s.HasErrors);
            });

            // -------------------------------------------------------------
            // 3. Local Library Table Mapping
            // -------------------------------------------------------------
            modelBuilder.Entity<LocalStashGame>(entity =>
            {
                ConfigureStashTableBase(entity, "local_stash");

                // Local-specific persisted properties
                entity.Property(g => g.PackageType);
                entity.Property(g => g.Folder);
                entity.Property(g => g.RootFolder);
                entity.Property(g => g.DateAdded)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null,
                        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null
                    );

                // Local-specific ignored runtime properties
                entity.Ignore(g => g.FolderDiskSpaceBytes);
                entity.Ignore(g => g.IsFolderValid);
            });

            // -------------------------------------------------------------
            // 4. Steam Library Table Mapping
            // -------------------------------------------------------------
            modelBuilder.Entity<SteamStashGame>(entity =>
            {
                ConfigureStashTableBase(entity, "steam_stash");

                // Steam-specific persisted properties
                entity.Property(g => g.ExtId);

                // Steam-specific ignored runtime properties
                entity.Ignore(g => g.PlaytimeMinutes);
            });

            // -------------------------------------------------------------
            // 5. Epic Library Table Mapping
            // -------------------------------------------------------------
            modelBuilder.Entity<EpicStashGame>(entity =>
            {
                ConfigureStashTableBase(entity, "epic_stash");

                // Epic-specific persisted properties
                entity.Property(g => g.Uid);
                entity.Property(g => g.CatalogItemId);
                entity.Property(g => g.Namespace);
                entity.Property(g => g.AppName);

                // Epic-specific ignored runtime properties
                entity.Ignore(g => g.FallbackCoverUrl);
                entity.Ignore(g => g.PlaytimeMinutes);
            });

            // -------------------------------------------------------------
            // 6. GOG Library Table Mapping
            // -------------------------------------------------------------
            modelBuilder.Entity<GogStashGame>(entity =>
            {
                ConfigureStashTableBase(entity, "gog_stash");

                // GOG-specific persisted properties
                entity.Property(g => g.ExtId);

                // GOG-specific ignored runtime properties
                entity.Ignore(g => g.FallbackCoverUrl);
                entity.Ignore(g => g.PlaytimeMinutes);
            });

            // -------------------------------------------------------------
            // 7. Amazon Library Table Mapping
            // -------------------------------------------------------------

            modelBuilder.Entity<AmazonStashGame>(entity =>
            {
                ConfigureStashTableBase(entity, "amazon_stash");

                // Amazon-specific persisted properties
                entity.Property(g => g.ExtId);

                // Amazon-specific ignored runtime properties
                entity.Ignore(g => g.FallbackCoverUrl);
            });
        }

        /// <summary>
        /// Configures shared table keys, common persisted columns, and non-persisted IGDB/UI properties for any StashGame entity.
        /// </summary>
        private static void ConfigureStashTableBase<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName) where TEntity : StashGame
        {
            entity.HasBaseType((Type)null!);
            entity.ToTable(tableName);
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Id).ValueGeneratedNever();

            // Common persisted columns across all stash tables
            entity.Property(g => g.Name);
            entity.Property(g => g.SortName);
            entity.Property(g => g.IsFavorite);
            entity.Property(g => g.IsInstalled);
            entity.Property(g => g.IsPlayed);
            entity.Property(g => g.IsPlaying);
            entity.Property(g => g.IsFinished);
            entity.Property(g => g.PlayedToEnd);
            entity.Property(g => g.IsInPlaylist);

            // Common UI & runtime state (Ignored by EF Core)
            entity.Ignore(g => g.MatchName);
            entity.Ignore(g => g.IsIgdbMatched);
            entity.Ignore(g => g.IsSelected);
            entity.Ignore(g => g.IsLoadingIgdbMatches);
            entity.Ignore(g => g.IsRefreshing);
            entity.Ignore(g => g.IgdbMatches);
            entity.Ignore(g => g.IgdbSelectedMatch);
            entity.Ignore(g => g.ReleaseDate);

            // IGDB v4 complex relations & graph properties (Ignored by EF Core)
            entity.Ignore(g => g.Cover);
            entity.Ignore(g => g.Screenshots);
            entity.Ignore(g => g.Videos);
            entity.Ignore(g => g.SimilarGames);
            entity.Ignore(g => g.InvolvedCompanies);
            entity.Ignore(g => g.Platforms);
            entity.Ignore(g => g.Genres);
            entity.Ignore(g => g.Themes);
            entity.Ignore(g => g.PlayerPerspectives);
            entity.Ignore(g => g.GameModes);
            entity.Ignore(g => g.Artworks);
            entity.Ignore(g => g.AgeRatings);
            entity.Ignore(g => g.AlternativeNames);
            entity.Ignore(g => g.Bundles);
            entity.Ignore(g => g.Collections);
            entity.Ignore(g => g.Dlcs);
            entity.Ignore(g => g.ExpandedGames);
            entity.Ignore(g => g.Expansions);
            entity.Ignore(g => g.ExternalGames);
            entity.Ignore(g => g.Forks);
            entity.Ignore(g => g.Franchise);
            entity.Ignore(g => g.Franchises);
            entity.Ignore(g => g.GameEngines);
            entity.Ignore(g => g.GameLocalizations);
            entity.Ignore(g => g.GameStatus);
            entity.Ignore(g => g.GameType);
            entity.Ignore(g => g.Keywords);
            entity.Ignore(g => g.LanguageSupports);
            entity.Ignore(g => g.MultiplayerModes);
            entity.Ignore(g => g.ParentGame);
            entity.Ignore(g => g.Ports);
            entity.Ignore(g => g.ReleaseDates);
            entity.Ignore(g => g.Remakes);
            entity.Ignore(g => g.Remasters);
            entity.Ignore(g => g.StandaloneExpansions);
            entity.Ignore(g => g.Tags);
            entity.Ignore(g => g.VersionParent);
            entity.Ignore(g => g.Websites);

            // Primitive IGDB metadata not persisted locally
            entity.Ignore(g => g.Summary);
            entity.Ignore(g => g.Storyline);
            entity.Ignore(g => g.Slug);
            entity.Ignore(g => g.Url);
            entity.Ignore(g => g.Checksum);
            entity.Ignore(g => g.CreatedAt);
            entity.Ignore(g => g.UpdatedAt);
            entity.Ignore(g => g.FirstReleaseDate);
            entity.Ignore(g => g.Rating);
            entity.Ignore(g => g.RatingCount);
            entity.Ignore(g => g.TotalRating);
            entity.Ignore(g => g.TotalRatingCount);
            entity.Ignore(g => g.AggregatedRating);
            entity.Ignore(g => g.AggregatedRatingCount);
            entity.Ignore(g => g.Hypes);
            entity.Ignore(g => g.VersionTitle);

            // Computed/Helper bridges
            entity.Ignore(g => g.DeveloperCompany);
        }

        public static GameStashDbContext CreateDbContext()
        {
            return new GameStashDbContext();
        }
    }
}
