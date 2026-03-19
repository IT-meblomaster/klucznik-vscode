using MojaAplikacja.Models;
using MySqlConnector;

namespace MojaAplikacja.Services;

public class KeyService
{
    private readonly string _connectionString;

    public KeyService()
    {
        var config = new DatabaseConfig();
        _connectionString = config.MariaDbConnectionString;
    }

    public async Task<List<KeyItem>> GetKeysAsync(string? searchText, bool onlyActive, bool onlyWithoutRfid)
    {
        var result = new List<KeyItem>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = """
            SELECT id, name, description, rfid_tag, is_active
            FROM `keys`
            WHERE 1 = 1
            """;

        if (onlyActive)
        {
            sql += " AND is_active = 1";
        }

        if (onlyWithoutRfid)
        {
            sql += " AND (rfid_tag IS NULL OR TRIM(rfid_tag) = '')";
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            sql += """
                 AND (
                    name LIKE @search
                    OR description LIKE @search
                    OR rfid_tag LIKE @search
                 )
                """;
        }

        sql += " ORDER BY name;";

        await using var command = new MySqlCommand(sql, connection);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            command.Parameters.AddWithValue("@search", $"%{searchText.Trim()}%");
        }

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new KeyItem
            {
                Id = reader.GetFieldValue<uint>(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                RfidTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4)
            });
        }

        return result;
    }

    public async Task<uint> InsertAsync(KeyItem item)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                INSERT INTO `keys` (name, description, rfid_tag, is_active)
                VALUES (@name, @description, @rfidTag, @isActive);
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@name", item.Name);
            command.Parameters.AddWithValue("@description", NormalizeText(item.Description));
            command.Parameters.AddWithValue("@rfidTag", NormalizeText(item.RfidTag));
            command.Parameters.AddWithValue("@isActive", item.IsActive);

            await command.ExecuteNonQueryAsync();

            var insertedId = (uint)command.LastInsertedId;

            await InsertLogAsync(
                connection,
                (MySqlTransaction)transaction,
                insertedId,
                "CREATE",
                $"Dodano klucz: {item.Name}");

            await transaction.CommitAsync();
            return insertedId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(KeyItem item)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                UPDATE `keys`
                SET
                    name = @name,
                    description = @description,
                    rfid_tag = @rfidTag,
                    is_active = @isActive
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", item.Id);
            command.Parameters.AddWithValue("@name", item.Name);
            command.Parameters.AddWithValue("@description", NormalizeText(item.Description));
            command.Parameters.AddWithValue("@rfidTag", NormalizeText(item.RfidTag));
            command.Parameters.AddWithValue("@isActive", item.IsActive);

            await command.ExecuteNonQueryAsync();

            await InsertLogAsync(
                connection,
                (MySqlTransaction)transaction,
                item.Id,
                "UPDATE",
                $"Zaktualizowano klucz: {item.Name}");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SoftDeleteAsync(uint id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                UPDATE `keys`
                SET is_active = 0
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();

            await InsertLogAsync(
                connection,
                (MySqlTransaction)transaction,
                id,
                "DEACTIVATE",
                "Dezaktywowano klucz");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task InsertLogAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint keyId,
        string actionType,
        string? actionDetails)
    {
        const string logSql = """
            INSERT INTO key_logs (key_id, action_type, action_details)
            VALUES (@keyId, @actionType, @actionDetails);
            """;

        await using var logCommand = new MySqlCommand(logSql, connection, transaction);
        logCommand.Parameters.AddWithValue("@keyId", keyId);
        logCommand.Parameters.AddWithValue("@actionType", actionType);
        logCommand.Parameters.AddWithValue("@actionDetails", NormalizeText(actionDetails));

        await logCommand.ExecuteNonQueryAsync();
    }

    private static object NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}