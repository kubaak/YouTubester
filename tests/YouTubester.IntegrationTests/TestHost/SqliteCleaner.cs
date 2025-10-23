using System.Data.Common;

namespace YouTubester.IntegrationTests.TestHost;

public static class SqliteCleaner
{
    public static async Task CleanAsync(DbConnection connection)
    {
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync();

            // Disable foreign keys temporarily
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "PRAGMA foreign_keys = OFF";
                await cmd.ExecuteNonQueryAsync();
            }

            // Get all user tables
            var tableNames = new List<string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var tableName = reader.GetString(0);
                    tableNames.Add(tableName);
                }
            }

            // Delete from all user tables
            foreach (var tableName in tableNames)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = $"DELETE FROM \"{tableName}\"";
                await cmd.ExecuteNonQueryAsync();
            }

            // Reset auto-increment sequences
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM sqlite_sequence";
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // sqlite_sequence might not exist if no tables use AUTOINCREMENT
                }
            }

            // Re-enable foreign keys
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }
}