using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameStash.Core.Services
{
    public static class PostgresDatabaseBootstrapper
    {
        public static async Task EnsureDatabaseExistsAsync(string targetConnectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetConnectionString))
                throw new ArgumentException("A PostgreSQL connection string is required.", nameof(targetConnectionString));

            var targetBuilder = new NpgsqlConnectionStringBuilder(targetConnectionString);
            if (string.IsNullOrWhiteSpace(targetBuilder.Database))
                throw new InvalidOperationException("The PostgreSQL connection string must specify a database name.");

            string databaseName = targetBuilder.Database;
            var maintenanceBuilder = new NpgsqlConnectionStringBuilder(targetConnectionString)
            {
                Database = "postgres",
                Pooling = false
            };

            await using var connection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using (var existsCommand = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @databaseName;",
                connection))
            {
                existsCommand.Parameters.AddWithValue("databaseName", databaseName);
                var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
                if (exists != null)
                    return;
            }

            string quotedDatabaseName = $"\"{databaseName.Replace("\"", "\"\"")}\"";
            await using var createCommand = new NpgsqlCommand($"CREATE DATABASE {quotedDatabaseName};", connection);

            try
            {
                await createCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
            {
                // Another application instance created the database between the existence check and CREATE DATABASE.
            }
        }
    }
}
