using GameStash.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GameStash.Core.Data
{
    public class GameStashDbContext : DbContext
    {
        public DbSet<StashGame> LocalLibrary { get; set; }
        public DbSet<Settings> Settings { get; set; }
        public DbSet<RootFolder> RootFolders { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseNpgsql("Host=localhost;Database=gamestash;Username=postgres;Password=postgres");

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
            });

            // -------------------------------------------------------------
            // 2. Local Library Table Mapping (Game entity)
            // -------------------------------------------------------------
            modelBuilder.Entity<RootFolder>(entity =>
            {
                entity.ToTable("root_folders");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(r => r.Path).HasColumnName("path").IsRequired();
                entity.Ignore(s => s.UsedSpace);
                entity.Ignore(s => s.FreeSpace);
                entity.Ignore(s => s.FoundGamesCount);
                entity.Ignore(s => s.IsImportingFolders);
                entity.Ignore(s => s.HasErrors);
            });

            // -------------------------------------------------------------
            // 3. Local Library Table Mapping (Game entity)
            // -------------------------------------------------------------
            modelBuilder.Entity<StashGame>(entity =>
            {
                entity.HasBaseType((Type)null);
                entity.ToTable("localLibrary");
                entity.HasKey(g => g.Id);
                entity.Property(g => g.Id).ValueGeneratedNever();

                // ---------------------------------------------------------
                // PERSISTED PROPERTIES (Actual columns in PostgreSQL)
                // ---------------------------------------------------------
                entity.Property(g => g.PackageType);
                entity.Property(g => g.Folder);
                entity.Property(g => g.RootFolder);
                entity.Property(g => g.Name);
                entity.Property(g => g.SortName);
                entity.Property(g => g.IsFavorite);
                entity.Property(g => g.IsInstalled);
                entity.Property(g => g.IsPlayed);
                entity.Property(g => g.IsPlaying);
                entity.Property(g => g.IsFinished);
                entity.Property(g => g.DateAdded)
                  .HasConversion(
                      v => v.HasValue ? v.Value.ToUniversalTime() : (DateTime?)null,
                      v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null
                  );

                // ---------------------------------------------------------
                // LOCAL UI & RUNTIME STATE (Ignored by EF Core)
                // ---------------------------------------------------------
                entity.Ignore(g => g.FolderDiskSpaceBytes);
                entity.Ignore(g => g.MatchName);
                entity.Ignore(g => g.IsIgdbMatched);
                entity.Ignore(g => g.IsSelected);
                entity.Ignore(g => g.IsFolderValid);
                entity.Ignore(g => g.IsLoadingIgdbMatches);
                entity.Ignore(g => g.IsRefreshing);
                entity.Ignore(g => g.IgdbMatches);
                entity.Ignore(g => g.IgdbSelectedMatch);
                entity.Ignore(g => g.ReleaseDate);

                // ---------------------------------------------------------
                // IGDB v4 COMPLEX RELATIONS & GRAPH PROPS (Ignored by EF Core)
                // ---------------------------------------------------------
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
                entity.Ignore(g => g.Forks);                     // v4: plural
                entity.Ignore(g => g.Franchise);
                entity.Ignore(g => g.Franchises);
                entity.Ignore(g => g.GameEngines);
                entity.Ignore(g => g.GameLocalizations);
                entity.Ignore(g => g.GameStatus);                // v4: IdentityOrValue<GameStatus>
                entity.Ignore(g => g.GameType);                  // v4: IdentityOrValue<GameType>
                entity.Ignore(g => g.Keywords);
                entity.Ignore(g => g.LanguageSupports);          // v4: new property
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

                // ---------------------------------------------------------
                // PRIMITIVE IGDB METADATA NOT PERSISTED LOCALLY
                // ---------------------------------------------------------
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
                entity.Ignore(g => g.ReleaseDate);
            });
        }

        public static GameStashDbContext CreateDbContext()
        {
            return new GameStashDbContext();
        }
    }
}