using GameStash.Core.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace GameStash.Core.Services
{
    public class SqliteHelper
    {
        private static readonly ILogger log = Log.ForContext<SqliteHelper>();

        private static string DbFilePath;

        // Method to initialize the database
        public static void InitializeDatabase(string dbFilePath)
        {
            DbFilePath = dbFilePath;

            log.Information("Initializing local storage database engine mapping rules...");

            // Check if the directory where the database file should be stored exists; if not, create it
            string directoryPath = Path.GetDirectoryName(DbFilePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                log.Warning("Target data storage directory missing. Creating directory space: {Path}", directoryPath);
                Directory.CreateDirectory(directoryPath); // Create the directory if it doesn't exist
            }

            // Microsoft.Data.Sqlite creates the .db file automatically when connection.Open() is invoked.
            // No manual file creation via CreateFile() is required anymore.
            CreateTables(); // Create tables after the directory space is secured
        }

        private static void CreateTables()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DbFilePath};"))
                {
                    connection.Open();
                    log.Debug("Local Sqlite connection channel opened and verified successfully.");

                    // 1. Create the table 'settings' if it does not already exist
                    string createSettingsTableQuery = @"
                        CREATE TABLE IF NOT EXISTS settings (
                            RootFolders TEXT,
                            IGDB_Client_ID TEXT,
                            IGDB_Secret TEXT
                        );";
                    ExecuteNonQuery(connection, createSettingsTableQuery);

                    // 2. Check if the table has any rows
                    string checkIfTableHasRowsQuery = "SELECT COUNT(*) FROM settings;";
                    int rowCount = ExecuteScalar<int>(connection, checkIfTableHasRowsQuery);

                    // 3. If the table has no rows, insert a row with NULL values
                    if (rowCount == 0)
                    {
                        string insertNullValuesQuery = "INSERT INTO settings (RootFolders, IGDB_Client_ID, IGDB_Secret) VALUES (NULL, NULL, NULL);";
                        ExecuteNonQuery(connection, insertNullValuesQuery);
                    }
                    // 4. If the table has more than 1 row, delete all but one row
                    else if (rowCount > 1)
                    {
                        // Delete all rows except the first one
                        string deleteExtraRowsQuery = "DELETE FROM settings WHERE ROWID NOT IN (SELECT ROWID FROM settings LIMIT 1);";
                        ExecuteNonQuery(connection, deleteExtraRowsQuery);
                    }

                    // 1. Create the table 'library' if it does not already exist
                    string createLibraryTableQuery = @"
                        CREATE TABLE IF NOT EXISTS library (
                            IGDB_Id INTEGER UNIQUE,
                            Type TEXT,
                            Folder TEXT,
                            RootFolder TEXT,
                            Name TEXT,
                            SortName TEXT,
                            IsFavorite INTEGER,
                            IsInstalled INTEGER,
                            DateAdded INTEGER
                        );";
                    ExecuteNonQuery(connection, createLibraryTableQuery);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "?? Critical breakdown occurred while processing structural database layout parameters");
                throw; // Re-throw the exception after logging it
            }
        }

        // Executes a query that returns a single value (such as COUNT or SUM)
        public static T ExecuteScalar<T>(SqliteConnection connection, string query)
        {
            using (var cmd = new SqliteCommand(query, connection))
            {
                object result = cmd.ExecuteScalar();

                // If the result is DBNull, return the default value for the type
                if (result == DBNull.Value || result == null)
                {
                    return default;
                }

                // Handle casting the result to the correct type
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)(Convert.ToInt32(result));
                }

                // Otherwise, just cast it directly to T
                return (T)result;
            }
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string query)
        {
            using (var command = new SqliteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        public static async Task<Settings> GetSettingsAsync()
        {
            using (var connection = new SqliteConnection($"Data Source={DbFilePath};"))
            {
                await connection.OpenAsync(); // Open the connection asynchronously

                var query = "SELECT * FROM Settings LIMIT 1"; // Query to select one row from Settings
                using (var command = new SqliteCommand(query, connection)) // Create the command
                {
                    using (var reader = await command.ExecuteReaderAsync()) // Execute the command and get the reader
                    {
                        if (await reader.ReadAsync()) // Read the first row
                        {
                            // Create a new Settings object and populate it from the reader
                            return new Settings
                            {
                                RootFolders = reader["RootFolders"]?.ToString(),
                                IGDB_Client_ID = reader["IGDB_Client_ID"]?.ToString(),
                                IGDB_Secret = reader["IGDB_Secret"]?.ToString()
                            };
                        }
                    }
                }
            }
            return null; // Return null if no settings are found
        }

        public static async Task<List<Game>> GetLibraryAsync()
        {
            var libraryEntries = new List<Game>(); // List to hold the results

            using (var connection = new SqliteConnection($"Data Source={DbFilePath};"))
            {
                await connection.OpenAsync(); // Open the connection asynchronously

                var query = "SELECT * FROM Library"; // Query to select all rows from Library
                using (var command = new SqliteCommand(query, connection)) // Create the command
                {
                    using (var reader = await command.ExecuteReaderAsync()) // Execute the command and get the reader
                    {
                        while (await reader.ReadAsync()) // Loop through all rows
                        {
                            var entry = new Game
                            {
                                Id = long.Parse(reader["IGDB_Id"].ToString()),
                                Type = reader["Type"]?.ToString(),
                                Folder = reader["Folder"]?.ToString(),
                                RootFolder = reader["RootFolder"]?.ToString(),
                                Name = reader["Name"]?.ToString(),
                                SortName = reader["SortName"]?.ToString(),
                                IsFavorite = reader["IsFavorite"]?.ToString() == "1",
                                IsInstalled = reader["IsInstalled"]?.ToString() == "1",
                                DateAdded = reader["DateAdded"] == DBNull.Value || string.IsNullOrEmpty(reader["DateAdded"].ToString())
                                    ? null
                                    : DateTime.ParseExact(reader["DateAdded"].ToString(), "ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture)
                            };
                            libraryEntries.Add(entry); // Add each entry to the list
                        }
                    }
                }
            }

            return libraryEntries; // Return the list of library entries
        }

        // Method to update a specific setting in the Settings table
        public static Task UpdateSettingsEntry(string columnName, string newValue)
        {
            return Task.Run(() =>
            {
                // SQL statement to update the value of a specific column
                string query = $"UPDATE Settings SET {columnName} = @newValue WHERE ROWID = 1";

                using (var connection = new SqliteConnection($"Data Source={DbFilePath};"))
                {
                    connection.Open(); // Open the connection

                    using (var command = new SqliteCommand(query, connection))
                    {
                        // Add the parameter to prevent SQL injection
                        command.Parameters.AddWithValue("@newValue", (object)newValue ?? DBNull.Value);

                        command.ExecuteNonQuery(); // Execute the update command
                    }
                }

            });
        }

        public static void InsertOrUpdateLibraryEntry(Game game)
        {
            using (var connection = new SqliteConnection($"Data Source={DbFilePath};"))
            {
                connection.Open();

                string selectQuery = "SELECT COUNT(*) FROM Library WHERE IGDB_Id = @IGDB_Id";
                using (var selectCommand = new SqliteCommand(selectQuery, connection))
                {
                    selectCommand.Parameters.AddWithValue("@IGDB_Id", game.Id);
                    var count = Convert.ToInt32(selectCommand.ExecuteScalar());

                    if (count > 0)
                    {
                        // UPDATE Query block mapping
                        string updateQuery = "UPDATE Library SET Type = @Type, Folder = @Folder, RootFolder = @RootFolder, Name = @Name, SortName = @SortName, IsFavorite = @IsFavorite, IsInstalled = @IsInstalled, DateAdded = @DateAdded WHERE IGDB_Id = @IGDB_Id";
                        using (var updateCommand = new SqliteCommand(updateQuery, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@Type", (object)game.Type ?? DBNull.Value);
                            updateCommand.Parameters.AddWithValue("@Folder", (object)game.Folder ?? DBNull.Value);
                            updateCommand.Parameters.AddWithValue("@RootFolder", (object)game.RootFolder ?? DBNull.Value);
                            updateCommand.Parameters.AddWithValue("@Name", (object)game.Name ?? DBNull.Value);
                            updateCommand.Parameters.AddWithValue("@SortName", (object)game.SortName ?? DBNull.Value);
                            updateCommand.Parameters.AddWithValue("@IsFavorite", game.IsFavorite ? 1 : 0);
                            updateCommand.Parameters.AddWithValue("@IsInstalled", game.IsInstalled ? 1 : 0);
                            updateCommand.Parameters.AddWithValue("@DateAdded", game.DateAdded == null ? DBNull.Value : (object)game.DateAdded.Value.ToString("ddMMyyyy"));
                            // Fixed variable assignment parameter matching core primary long ID keys
                            updateCommand.Parameters.AddWithValue("@IGDB_Id", game.Id);
                            updateCommand.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // INSERT Query block mapping
                        string insertQuery = "INSERT INTO Library (IGDB_Id, Type, Folder, RootFolder, Name, SortName, IsFavorite, IsInstalled, DateAdded) VALUES (@IGDB_Id, @Type, @Folder, @RootFolder, @Name, @SortName, @IsFavorite, @IsInstalled, @DateAdded)";
                        using (var insertCommand = new SqliteCommand(insertQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@IGDB_Id", game.Id);
                            insertCommand.Parameters.AddWithValue("@Type", (object)game.Type ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@Folder", (object)game.Folder ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@RootFolder", (object)game.RootFolder ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@Name", (object)game.Name ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@SortName", (object)game.SortName ?? DBNull.Value);
                            insertCommand.Parameters.AddWithValue("@IsFavorite", game.IsFavorite ? 1 : 0);
                            insertCommand.Parameters.AddWithValue("@IsInstalled", game.IsInstalled ? 1 : 0);
                            insertCommand.Parameters.AddWithValue("@DateAdded", game.DateAdded == null ? DBNull.Value : (object)game.DateAdded.Value.ToString("ddMMyyyy"));
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
        public static void RemoveEntryFromLibrary(Game game)
        {
            using (var connection = new SqliteConnection($"Data Source={DbFilePath};"))
            {
                connection.Open();
                string deleteQuery = "DELETE FROM Library WHERE IGDB_Id = @IGDB_Id";
                using (var command = new SqliteCommand(deleteQuery, connection))
                {
                    command.Parameters.AddWithValue("@IGDB_Id", game.Id);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}