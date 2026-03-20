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

    public async Task<List<KeyItem>> GetKeysAsync()
    {
        var result = new List<KeyItem>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                k.id,
                k.name,
                k.description,
                k.rfid_tag,
                k.is_active,
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM key_loans kl
                        WHERE kl.key_id = k.id
                          AND kl.returned_at IS NULL
                    ) THEN 1
                    ELSE 0
                END AS is_issued
            FROM `keys` k
            WHERE k.is_active = 1
            ORDER BY k.name;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new KeyItem
            {
                Id = reader.GetFieldValue<uint>(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                RfidTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4),
                IsIssued = !reader.IsDBNull(5) && reader.GetBoolean(5)
            });
        }

        return result;
    }

    public async Task<KeyItem?> GetKeyByRfidAsync(string rfidTag)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                k.id,
                k.name,
                k.description,
                k.rfid_tag,
                k.is_active,
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM key_loans kl
                        WHERE kl.key_id = k.id
                          AND kl.returned_at IS NULL
                    ) THEN 1
                    ELSE 0
                END AS is_issued
            FROM `keys` k
            WHERE k.is_active = 1
              AND k.rfid_tag = @rfidTag
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@rfidTag", rfidTag.Trim());

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new KeyItem
            {
                Id = reader.GetFieldValue<uint>(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                RfidTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4),
                IsIssued = !reader.IsDBNull(5) && reader.GetBoolean(5)
            };
        }

        return null;
    }

    public async Task<uint> InsertAsync(string name, string? description)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                INSERT INTO `keys` (name, description, rfid_tag, is_active)
                VALUES (@name, @description, NULL, 1);
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@description", NormalizeText(description));

            await command.ExecuteNonQueryAsync();
            var insertedId = (uint)command.LastInsertedId;

            await InsertLogAsync(connection, (MySqlTransaction)transaction, insertedId, "CREATE", $"Dodano klucz: {name.Trim()}");

            await transaction.CommitAsync();
            return insertedId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(uint id, string name, string? description, bool removeRfid)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            string sql;

            if (removeRfid)
            {
                sql = """
                    UPDATE `keys`
                    SET
                        name = @name,
                        description = @description,
                        rfid_tag = NULL
                    WHERE id = @id;
                    """;
            }
            else
            {
                sql = """
                    UPDATE `keys`
                    SET
                        name = @name,
                        description = @description
                    WHERE id = @id;
                    """;
            }

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@description", NormalizeText(description));

            await command.ExecuteNonQueryAsync();

            await InsertLogAsync(connection, (MySqlTransaction)transaction, id, "UPDATE", $"Zaktualizowano klucz: {name.Trim()}");

            if (removeRfid)
            {
                await InsertLogAsync(connection, (MySqlTransaction)transaction, id, "REMOVE_RFID", "Usunięto przypisany RFID");
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AssignRfidAsync(uint id, string rfidTag)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                UPDATE `keys`
                SET rfid_tag = @rfidTag
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@rfidTag", rfidTag.Trim());

            await command.ExecuteNonQueryAsync();

            await InsertLogAsync(connection, (MySqlTransaction)transaction, id, "ASSIGN_RFID", $"Przypisano RFID: {rfidTag.Trim()}");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RemoveRfidAsync(uint id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                UPDATE `keys`
                SET rfid_tag = NULL
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();

            await InsertLogAsync(connection, (MySqlTransaction)transaction, id, "REMOVE_RFID", "Usunięto RFID");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(uint id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                DELETE FROM `keys`
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<string?> GetKeyNameByRfidAsync(string rfidTag)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT name
            FROM `keys`
            WHERE rfid_tag = @rfidTag
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@rfidTag", rfidTag.Trim());

        var result = await command.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<KeyLoanOperationResult> RegisterIssueOrReturnAsync(KeyItem key, PersonResult person)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string openLoanSql = """
                SELECT id, issued_to_name
                FROM key_loans
                WHERE key_id = @keyId
                  AND returned_at IS NULL
                LIMIT 1
                FOR UPDATE;
                """;

            await using var openLoanCommand = new MySqlCommand(openLoanSql, connection, (MySqlTransaction)transaction);
            openLoanCommand.Parameters.AddWithValue("@keyId", key.Id);

            await using var reader = await openLoanCommand.ExecuteReaderAsync();

            ulong? openLoanId = null;
            string? issuedToName = null;

            if (await reader.ReadAsync())
            {
                openLoanId = reader.GetFieldValue<ulong>(0);
                issuedToName = reader.IsDBNull(1) ? null : reader.GetString(1);
            }

            await reader.CloseAsync();

            if (openLoanId is null)
            {
                const string insertLoanSql = """
                    INSERT INTO key_loans
                        (key_id, issued_to_card, issued_to_name, issued_at)
                    VALUES
                        (@keyId, @issuedToCard, @issuedToName, NOW());
                    """;

                await using var insertLoanCommand = new MySqlCommand(insertLoanSql, connection, (MySqlTransaction)transaction);
                insertLoanCommand.Parameters.AddWithValue("@keyId", key.Id);
                insertLoanCommand.Parameters.AddWithValue("@issuedToCard", person.CardNumber);
                insertLoanCommand.Parameters.AddWithValue("@issuedToName", $"{person.FirstName} {person.LastName}".Trim());

                await insertLoanCommand.ExecuteNonQueryAsync();

                await InsertLogAsync(
                    connection,
                    (MySqlTransaction)transaction,
                    key.Id,
                    "ISSUE",
                    $"Wydano klucz {key.Name} osobie {person.FirstName} {person.LastName}".Trim());

                await transaction.CommitAsync();

                return new KeyLoanOperationResult
                {
                    IsIssue = true,
                    Message = $"Wydano klucz: {key.Name} -> {person.FirstName} {person.LastName}".Trim()
                };
            }
            else
            {
                const string returnLoanSql = """
                    UPDATE key_loans
                    SET
                        returned_by_card = @returnedByCard,
                        returned_by_name = @returnedByName,
                        returned_at = NOW()
                    WHERE id = @loanId;
                    """;

                await using var returnLoanCommand = new MySqlCommand(returnLoanSql, connection, (MySqlTransaction)transaction);
                returnLoanCommand.Parameters.AddWithValue("@loanId", openLoanId.Value);
                returnLoanCommand.Parameters.AddWithValue("@returnedByCard", person.CardNumber);
                returnLoanCommand.Parameters.AddWithValue("@returnedByName", $"{person.FirstName} {person.LastName}".Trim());

                await returnLoanCommand.ExecuteNonQueryAsync();

                await InsertLogAsync(
                    connection,
                    (MySqlTransaction)transaction,
                    key.Id,
                    "RETURN",
                    $"Zwrócono klucz {key.Name}. Wydał: {issuedToName ?? "nieznany"}, zwrócił: {person.FirstName} {person.LastName}".Trim());

                await transaction.CommitAsync();

                return new KeyLoanOperationResult
                {
                    IsReturn = true,
                    Message = $"Zwrócono klucz: {key.Name} <- {person.FirstName} {person.LastName}".Trim()
                };
            }
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
        const string sql = """
            INSERT INTO key_logs (key_id, action_type, action_details)
            VALUES (@keyId, @actionType, @actionDetails);
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@keyId", keyId);
        command.Parameters.AddWithValue("@actionType", actionType);
        command.Parameters.AddWithValue("@actionDetails", NormalizeText(actionDetails));

        await command.ExecuteNonQueryAsync();
    }

    private static object NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}