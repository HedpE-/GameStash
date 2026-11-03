using GameStash.Core.Models;
using Npgsql;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameStash.Core.Services
{
    public class PostgresHelper
    {
        private static readonly ILogger log = Log.ForContext<PostgresHelper>();
        private static string _connectionString = string.Empty;

        /// <summary>
        /// Initializes the PostgreSQL database connection and creates required tables.
        /// </summary>
        /// <param name="connectionString">Standard PostgreSQL connection string.</param>
        public static void InitializeDatabase(string connectionString)
        {
            _connectionString = connectionString;

            log.Information("Initializing PostgreSQL database engine mapping rules...");

            CreateTables();
        }

        private static void CreateTables()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                log.Debug("PostgreSQL connection channel opened and verified successfully.");

                // 1. Create the 'settings' table if it does not already exist
                const string createSettingsTableQuery = @"
                    CREATE TABLE IF NOT EXISTS settings (
                        id SERIAL PRIMARY KEY,
                        root_folders TEXT,
                        igdb_client_id TEXT,
                        igdb_secret TEXT
                    );";
                ExecuteNonQuery(connection, createSettingsTableQuery);

                // 2. Ensure exactly one configuration row exists
                const string checkSettingsRowCountQuery = "SELECT COUNT(*) FROM settings;";
                long rowCount = ExecuteScalar<long>(connection, checkSettingsRowCountQuery);

                if (rowCount == 0)
                {
                    const string insertDefaultSettingsQuery = "INSERT INTO settings (root_folders, igdb_client_id, igdb_secret) VALUES (NULL, NULL, NULL);";
                    ExecuteNonQuery(connection, insertDefaultSettingsQuery);
                }
                else if (rowCount > 1)
                {
                    // Clean up duplicate configuration rows retaining only the lowest ID
                    const string deleteExtraRowsQuery = "DELETE FROM settings WHERE id NOT IN (SELECT id FROM settings ORDER BY id ASC LIMIT 1);";
                    ExecuteNonQuery(connection, deleteExtraRowsQuery);
                }

                // 3. Create the 'library' table if it does not already exist
                const string createLibraryTableQuery = @"
                    CREATE TABLE IF NOT EXISTS library (
                        igdb_id BIGINT PRIMARY KEY,
                        type TEXT,
                        folder TEXT,
                        root_folder TEXT,
                        name TEXT,
                        sort_name TEXT,
                        is_favorite BOOLEAN DEFAULT FALSE,
                        is_installed BOOLEAN DEFAULT FALSE,
                        date_added VARCHAR(10)
                    );";
                ExecuteNonQuery(connection, createLibraryTableQuery);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Critical breakdown occurred while processing structural PostgreSQL layout parameters.");
                throw;
            }
        }

        public static T ExecuteScalar<T>(NpgsqlConnection connection, string query)
        {
            using var cmd = new NpgsqlCommand(query, connection);
            object? result = cmd.ExecuteScalar();

            if (result == DBNull.Value || result == null)
            {
                return default!;
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)Convert.ToInt32(result);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)Convert.ToInt64(result);
            }

            return (T)result;
        }

        private static void ExecuteNonQuery(NpgsqlConnection connection, string query)
        {
            using var command = new NpgsqlCommand(query, connection);
            command.ExecuteNonQuery();
        }

        public static async Task<Settings?> GetSettingsAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = "SELECT root_folders, igdb_client_id, igdb_secret FROM settings ORDER BY id ASC LIMIT 1;";
            await using var command = new NpgsqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Settings
                {
                    IGDB_Client_ID = reader["igdb_client_id"]?.ToString(),
                    IGDB_Secret = reader["igdb_secret"]?.ToString()
                };
            }

            return null;
        }

        public static async Task<List<StashGame>> GetLibraryAsync()
        {
            var libraryEntries = new List<StashGame>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = "SELECT igdb_id, type, folder, root_folder, name, sort_name, is_favorite, is_installed, date_added FROM library;";
            await using var command = new NpgsqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var dateAddedRaw = reader["date_added"]?.ToString();
                DateTime? dateAdded = null;

                if (!string.IsNullOrEmpty(dateAddedRaw))
                {
                    if (DateTime.TryParseExact(dateAddedRaw, "ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
                    {
                        dateAdded = parsedDate;
                    }
                }

                var entry = new StashGame
                {
                    Id = Convert.ToInt64(reader["igdb_id"]),
                    PackageType = reader["type"]?.ToString(),
                    Folder = reader["folder"]?.ToString(),
                    RootFolder = reader["root_folder"]?.ToString(),
                    Name = reader["name"]?.ToString(),
                    SortName = reader["sort_name"]?.ToString(),
                    IsFavorite = reader["is_favorite"] is bool fav && fav,
                    IsInstalled = reader["is_installed"] is bool inst && inst,
                    DateAdded = dateAdded
                };

                libraryEntries.Add(entry);
            }

            return libraryEntries;
        }

        public static async Task UpdateSettingsEntry(string columnName, string? newValue)
        {
            // Map column names to PostgreSQL lowercase conventions if needed
            string targetColumn = columnName.ToLower() switch
            {
                "rootfolders" => "root_folders",
                "igdb_client_id" => "igdb_client_id",
                "igdb_secret" => "igdb_secret",
                _ => columnName
            };

            string query = $"UPDATE settings SET {targetColumn} = @newValue WHERE id = (SELECT id FROM settings ORDER BY id ASC LIMIT 1);";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@newValue", (object?)newValue ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Inserts or updates a library game record using PostgreSQL native atomic upsert (ON CONFLICT).
        /// </summary>
        public static void InsertOrUpdateLibraryEntry(StashGame game)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            const string upsertQuery = @"
                INSERT INTO library (igdb_id, type, folder, root_folder, name, sort_name, is_favorite, is_installed, date_added)
                VALUES (@igdb_id, @type, @folder, @root_folder, @name, @sort_name, @is_favorite, @is_installed, @date_added)
                ON CONFLICT (igdb_id) DO UPDATE SET
                    type = EXCLUDED.type,
                    folder = EXCLUDED.folder,
                    root_folder = EXCLUDED.root_folder,
                    name = EXCLUDED.name,
                    sort_name = EXCLUDED.sort_name,
                    is_favorite = EXCLUDED.is_favorite,
                    is_installed = EXCLUDED.is_installed,
                    date_added = EXCLUDED.date_added;";

            using var command = new NpgsqlCommand(upsertQuery, connection);

            command.Parameters.AddWithValue("@igdb_id", game.Id ?? 0);
            command.Parameters.AddWithValue("@type", (object?)game.PackageType ?? DBNull.Value);
            command.Parameters.AddWithValue("@folder", (object?)game.Folder ?? DBNull.Value);
            command.Parameters.AddWithValue("@root_folder", (object?)game.RootFolder ?? DBNull.Value);
            command.Parameters.AddWithValue("@name", (object?)game.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("@sort_name", (object?)game.SortName ?? DBNull.Value);
            command.Parameters.AddWithValue("@is_favorite", game.IsFavorite);
            command.Parameters.AddWithValue("@is_installed", game.IsInstalled);
            command.Parameters.AddWithValue("@date_added", game.DateAdded.HasValue
                ? (object)game.DateAdded.Value.ToString("ddMMyyyy")
                : DBNull.Value);

            command.ExecuteNonQuery();
        }

        public static void RemoveEntryFromLibrary(StashGame game)
        {
            if (!game.Id.HasValue) return;

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            const string deleteQuery = "DELETE FROM library WHERE igdb_id = @igdb_id;";
            using var command = new NpgsqlCommand(deleteQuery, connection);
            command.Parameters.AddWithValue("@igdb_id", game.Id.Value);
            command.ExecuteNonQuery();
        }
    }
}