using MySqlConnector;

namespace Klucznik.Services;

public class MariaDbTestService
{
    private readonly string _connectionString;

    public MariaDbTestService()
    {
        var config = new DatabaseConfig();
        _connectionString = config.MariaDbConnectionString;
    }

    public async Task<List<string>> GetTableNamesAsync()
    {
        var result = new List<string>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
            ORDER BY table_name;";

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
