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
                k.budynek,
                k.zawieszka,
                k.description,
                r.rfid_code,
                a.rfid_tag_id,
                k.is_active,
                CASE WHEN kl.id IS NOT NULL THEN 1 ELSE 0 END AS is_issued,
                kl.issued_to_name,
                kl.issued_at
            FROM `keys` k
            LEFT JOIN key_rfid_assignments a
                ON a.key_id = k.id
               AND a.assigned_to IS NULL
            LEFT JOIN rfid_tags r
                ON r.id = a.rfid_tag_id
            LEFT JOIN key_loans kl
                ON kl.key_id = k.id
               AND kl.returned_at IS NULL
            WHERE k.is_active = 1
            ORDER BY k.budynek, k.name;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(ReadKeyItem(reader));
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
                k.budynek,
                k.zawieszka,
                k.description,
                r.rfid_code,
                a.rfid_tag_id,
                k.is_active,
                CASE WHEN kl.id IS NOT NULL THEN 1 ELSE 0 END AS is_issued,
                kl.issued_to_name,
                kl.issued_at
            FROM rfid_tags r
            JOIN key_rfid_assignments a
                ON a.rfid_tag_id = r.id
               AND a.assigned_to IS NULL
            JOIN `keys` k
                ON k.id = a.key_id
            LEFT JOIN key_loans kl
                ON kl.key_id = k.id
               AND kl.returned_at IS NULL
            WHERE r.rfid_code = @rfidTag
              AND r.status = 'ACTIVE'
              AND k.is_active = 1
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@rfidTag", rfidTag.Trim());

        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? ReadKeyItem(reader) : null;
    }

    public async Task<List<KeyLoanReportItem>> GetLoanReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? userFilter,
        string? keyFilter)
    {
        var result = new List<KeyLoanReportItem>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                report.event_time,
                report.event_type,
                report.key_name,
                report.building,
                report.user_name,
                report.user_card,
                report.rfid_code
            FROM
            (
                SELECT
                    kl.issued_at AS event_time,
                    'Pobranie' AS event_type,
                    k.name AS key_name,
                    k.budynek AS building,
                    kl.issued_to_name AS user_name,
                    kl.issued_to_card AS user_card,
                    r.rfid_code AS rfid_code
                FROM key_loans kl
                JOIN `keys` k ON k.id = kl.key_id
                LEFT JOIN rfid_tags r ON r.id = kl.rfid_tag_id

                UNION ALL

                SELECT
                    kl.returned_at AS event_time,
                    'Zwrot' AS event_type,
                    k.name AS key_name,
                    k.budynek AS building,
                    kl.returned_by_name AS user_name,
                    kl.returned_by_card AS user_card,
                    r.rfid_code AS rfid_code
                FROM key_loans kl
                JOIN `keys` k ON k.id = kl.key_id
                LEFT JOIN rfid_tags r ON r.id = kl.rfid_tag_id
                WHERE kl.returned_at IS NOT NULL
            ) report
            WHERE (@dateFrom IS NULL OR report.event_time >= @dateFrom)
              AND (@dateTo IS NULL OR report.event_time < DATE_ADD(@dateTo, INTERVAL 1 DAY))
              AND (@userFilter IS NULL OR report.user_name LIKE CONCAT('%', @userFilter, '%'))
              AND (@keyFilter IS NULL OR report.key_name LIKE CONCAT('%', @keyFilter, '%'))
            ORDER BY report.event_time DESC;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@dateFrom", ToDbValue(dateFrom));
        command.Parameters.AddWithValue("@dateTo", ToDbValue(dateTo));
        command.Parameters.AddWithValue("@userFilter", NormalizeFilter(userFilter));
        command.Parameters.AddWithValue("@keyFilter", NormalizeFilter(keyFilter));

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new KeyLoanReportItem
            {
                EventTime = reader.GetDateTime(0),
                EventType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                KeyName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Building = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                UserName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                UserCard = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                RfidCode = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return result;
    }

    public async Task<uint> InsertAsync(string name, string? building, string? hanger, string? description)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                INSERT INTO `keys` (name, budynek, zawieszka, description, is_active)
                VALUES (@name, @building, @hanger, @description, 1);
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@building", NormalizeText(building));
            command.Parameters.AddWithValue("@hanger", NormalizeText(hanger));
            command.Parameters.AddWithValue("@description", NormalizeText(description));

            await command.ExecuteNonQueryAsync();
            var insertedId = (uint)command.LastInsertedId;

            await InsertLogAsync(connection, (MySqlTransaction)transaction, insertedId, null, "CREATE", $"Dodano klucz: {name.Trim()}");

            await transaction.CommitAsync();
            return insertedId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(uint id, string name, string? building, string? hanger, string? description, bool removeRfid)
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
                    budynek = @building,
                    zawieszka = @hanger,
                    description = @description
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@building", NormalizeText(building));
            command.Parameters.AddWithValue("@hanger", NormalizeText(hanger));
            command.Parameters.AddWithValue("@description", NormalizeText(description));

            await command.ExecuteNonQueryAsync();

            await InsertLogAsync(connection, (MySqlTransaction)transaction, id, null, "UPDATE", $"Zaktualizowano klucz: {name.Trim()}");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AssignRfidAsync(uint keyId, string rfidTag)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string selectKeyAssignmentSql = """
                SELECT id, rfid_tag_id
                FROM key_rfid_assignments
                WHERE key_id = @keyId
                  AND assigned_to IS NULL
                LIMIT 1
                FOR UPDATE;
                """;

            await using var selectKeyAssignmentCommand = new MySqlCommand(selectKeyAssignmentSql, connection, (MySqlTransaction)transaction);
            selectKeyAssignmentCommand.Parameters.AddWithValue("@keyId", keyId);

            ulong? currentAssignmentId = null;
            uint? currentRfidTagId = null;

            await using (var reader = await selectKeyAssignmentCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    currentAssignmentId = reader.GetFieldValue<ulong>(0);
                    currentRfidTagId = reader.GetFieldValue<uint>(1);
                }
            }

            uint rfidTagId;

            const string selectTagSql = """
                SELECT id
                FROM rfid_tags
                WHERE rfid_code = @rfidCode
                LIMIT 1
                FOR UPDATE;
                """;

            await using (var selectTagCommand = new MySqlCommand(selectTagSql, connection, (MySqlTransaction)transaction))
            {
                selectTagCommand.Parameters.AddWithValue("@rfidCode", rfidTag.Trim());
                var scalar = await selectTagCommand.ExecuteScalarAsync();

                if (scalar is null)
                {
                    const string insertTagSql = """
                        INSERT INTO rfid_tags (rfid_code, status)
                        VALUES (@rfidCode, 'ACTIVE');
                        """;

                    await using var insertTagCommand = new MySqlCommand(insertTagSql, connection, (MySqlTransaction)transaction);
                    insertTagCommand.Parameters.AddWithValue("@rfidCode", rfidTag.Trim());
                    await insertTagCommand.ExecuteNonQueryAsync();
                    rfidTagId = (uint)insertTagCommand.LastInsertedId;
                }
                else
                {
                    rfidTagId = Convert.ToUInt32(scalar);

                    const string updateTagSql = """
                        UPDATE rfid_tags
                        SET status = 'ACTIVE', updated_at = CURRENT_TIMESTAMP()
                        WHERE id = @rfidTagId;
                        """;

                    await using var updateTagCommand = new MySqlCommand(updateTagSql, connection, (MySqlTransaction)transaction);
                    updateTagCommand.Parameters.AddWithValue("@rfidTagId", rfidTagId);
                    await updateTagCommand.ExecuteNonQueryAsync();
                }
            }

            const string checkRfidInUseSql = """
                SELECT key_id
                FROM key_rfid_assignments
                WHERE rfid_tag_id = @rfidTagId
                  AND assigned_to IS NULL
                LIMIT 1
                FOR UPDATE;
                """;

            await using (var checkRfidInUseCommand = new MySqlCommand(checkRfidInUseSql, connection, (MySqlTransaction)transaction))
            {
                checkRfidInUseCommand.Parameters.AddWithValue("@rfidTagId", rfidTagId);
                var existingKeyIdObj = await checkRfidInUseCommand.ExecuteScalarAsync();

                if (existingKeyIdObj is not null)
                {
                    var existingKeyId = Convert.ToUInt32(existingKeyIdObj);

                    if (existingKeyId != keyId)
                        throw new InvalidOperationException("To RFID jest już aktywnie przypisane do innego klucza.");
                }
            }

            if (currentAssignmentId.HasValue)
            {
                if (currentRfidTagId == rfidTagId)
                    return;

                const string closeCurrentAssignmentSql = """
                    UPDATE key_rfid_assignments
                    SET assigned_to = NOW(), unassigned_reason = 'REASSIGN'
                    WHERE id = @assignmentId;
                    """;

                await using var closeCurrentAssignmentCommand = new MySqlCommand(closeCurrentAssignmentSql, connection, (MySqlTransaction)transaction);
                closeCurrentAssignmentCommand.Parameters.AddWithValue("@assignmentId", currentAssignmentId.Value);
                await closeCurrentAssignmentCommand.ExecuteNonQueryAsync();
            }

            const string insertAssignmentSql = """
                INSERT INTO key_rfid_assignments (key_id, rfid_tag_id, assigned_from, assigned_by, notes)
                VALUES (@keyId, @rfidTagId, NOW(), 'SYSTEM', NULL);
                """;

            await using (var insertAssignmentCommand = new MySqlCommand(insertAssignmentSql, connection, (MySqlTransaction)transaction))
            {
                insertAssignmentCommand.Parameters.AddWithValue("@keyId", keyId);
                insertAssignmentCommand.Parameters.AddWithValue("@rfidTagId", rfidTagId);
                await insertAssignmentCommand.ExecuteNonQueryAsync();
            }

            var keyName = await GetKeyNameInternalAsync(connection, (MySqlTransaction)transaction, keyId);

            await InsertLogAsync(connection, (MySqlTransaction)transaction, keyId, rfidTagId, "ASSIGN_RFID", $"Przypisano RFID {rfidTag.Trim()} do klucza {keyName}");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RemoveRfidAsync(uint keyId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string selectAssignmentSql = """
                SELECT id, rfid_tag_id
                FROM key_rfid_assignments
                WHERE key_id = @keyId
                  AND assigned_to IS NULL
                LIMIT 1
                FOR UPDATE;
                """;

            await using var selectAssignmentCommand = new MySqlCommand(selectAssignmentSql, connection, (MySqlTransaction)transaction);
            selectAssignmentCommand.Parameters.AddWithValue("@keyId", keyId);

            ulong? assignmentId = null;
            uint? rfidTagId = null;

            await using (var reader = await selectAssignmentCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    assignmentId = reader.GetFieldValue<ulong>(0);
                    rfidTagId = reader.GetFieldValue<uint>(1);
                }
            }

            if (!assignmentId.HasValue)
                throw new InvalidOperationException("Wybrany klucz nie ma przypisanego RFID.");

            const string closeAssignmentSql = """
                UPDATE key_rfid_assignments
                SET assigned_to = NOW(), unassigned_reason = 'MANUAL_REMOVE'
                WHERE id = @assignmentId;
                """;

            await using (var closeAssignmentCommand = new MySqlCommand(closeAssignmentSql, connection, (MySqlTransaction)transaction))
            {
                closeAssignmentCommand.Parameters.AddWithValue("@assignmentId", assignmentId.Value);
                await closeAssignmentCommand.ExecuteNonQueryAsync();
            }

            var keyName = await GetKeyNameInternalAsync(connection, (MySqlTransaction)transaction, keyId);

            await InsertLogAsync(connection, (MySqlTransaction)transaction, keyId, rfidTagId, "REMOVE_RFID", $"Usunięto aktywne przypisanie RFID z klucza {keyName}");

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
            SELECT k.name
            FROM rfid_tags r
            JOIN key_rfid_assignments a
                ON a.rfid_tag_id = r.id
               AND a.assigned_to IS NULL
            JOIN `keys` k
                ON k.id = a.key_id
            WHERE r.rfid_code = @rfidTag
              AND r.status = 'ACTIVE'
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
            uint? activeRfidTagId = await GetActiveRfidTagIdInternalAsync(connection, (MySqlTransaction)transaction, key.Id);

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
                    INSERT INTO key_loans (key_id, rfid_tag_id, issued_to_card, issued_to_name, issued_at)
                    VALUES (@keyId, @rfidTagId, @issuedToCard, @issuedToName, NOW());
                    """;

                await using var insertLoanCommand = new MySqlCommand(insertLoanSql, connection, (MySqlTransaction)transaction);
                insertLoanCommand.Parameters.AddWithValue("@keyId", key.Id);
                insertLoanCommand.Parameters.AddWithValue("@rfidTagId", ToDbValue(activeRfidTagId));
                insertLoanCommand.Parameters.AddWithValue("@issuedToCard", person.CardNumber);
                insertLoanCommand.Parameters.AddWithValue("@issuedToName", $"{person.FirstName} {person.LastName}".Trim());

                await insertLoanCommand.ExecuteNonQueryAsync();

                await InsertLogAsync(connection, (MySqlTransaction)transaction, key.Id, activeRfidTagId, "ISSUE", $"Wydano klucz {key.KeyWithBuildingDisplay} osobie {person.FirstName} {person.LastName}".Trim());

                await transaction.CommitAsync();

                return new KeyLoanOperationResult
                {
                    IsIssue = true,
                    Message = $"Wydano klucz: {key.KeyWithBuildingDisplay} -> {person.FirstName} {person.LastName}".Trim()
                };
            }

            const string returnLoanSql = """
                UPDATE key_loans
                SET returned_by_card = @returnedByCard,
                    returned_by_name = @returnedByName,
                    returned_at = NOW()
                WHERE id = @loanId;
                """;

            await using var returnLoanCommand = new MySqlCommand(returnLoanSql, connection, (MySqlTransaction)transaction);
            returnLoanCommand.Parameters.AddWithValue("@loanId", openLoanId.Value);
            returnLoanCommand.Parameters.AddWithValue("@returnedByCard", person.CardNumber);
            returnLoanCommand.Parameters.AddWithValue("@returnedByName", $"{person.FirstName} {person.LastName}".Trim());

            await returnLoanCommand.ExecuteNonQueryAsync();

            await InsertLogAsync(connection, (MySqlTransaction)transaction, key.Id, activeRfidTagId, "RETURN", $"Zwrócono klucz {key.KeyWithBuildingDisplay}. Wydał: {issuedToName ?? "nieznany"}, zwrócił: {person.FirstName} {person.LastName}".Trim());

            await transaction.CommitAsync();

            return new KeyLoanOperationResult
            {
                IsReturn = true,
                Message = $"Zwrócono klucz: {key.KeyWithBuildingDisplay} <- {person.FirstName} {person.LastName}".Trim()
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static KeyItem ReadKeyItem(MySqlDataReader reader)
    {
        return new KeyItem
        {
            Id = reader.GetFieldValue<uint>(0),
            Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Building = reader.IsDBNull(2) ? null : reader.GetString(2),
            Hanger = reader.IsDBNull(3) ? null : reader.GetString(3),
            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
            RfidTag = reader.IsDBNull(5) ? null : reader.GetString(5),
            CurrentRfidTagId = reader.IsDBNull(6) ? null : reader.GetFieldValue<uint>(6),
            IsActive = !reader.IsDBNull(7) && reader.GetBoolean(7),
            IsIssued = !reader.IsDBNull(8) && reader.GetBoolean(8),
            IssuedToName = reader.IsDBNull(9) ? null : reader.GetString(9),
            IssuedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
        };
    }

    private static async Task InsertLogAsync(MySqlConnection connection, MySqlTransaction transaction, uint keyId, uint? rfidTagId, string actionType, string? actionDetails)
    {
        const string sql = """
            INSERT INTO key_logs (key_id, rfid_tag_id, action_type, action_details)
            VALUES (@keyId, @rfidTagId, @actionType, @actionDetails);
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@keyId", keyId);
        command.Parameters.AddWithValue("@rfidTagId", ToDbValue(rfidTagId));
        command.Parameters.AddWithValue("@actionType", actionType);
        command.Parameters.AddWithValue("@actionDetails", NormalizeText(actionDetails));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetKeyNameInternalAsync(MySqlConnection connection, MySqlTransaction transaction, uint keyId)
    {
        const string sql = """
            SELECT name
            FROM `keys`
            WHERE id = @keyId
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@keyId", keyId);

        var result = await command.ExecuteScalarAsync();
        return result as string ?? $"ID {keyId}";
    }

    private static async Task<uint?> GetActiveRfidTagIdInternalAsync(MySqlConnection connection, MySqlTransaction transaction, uint keyId)
    {
        const string sql = """
            SELECT rfid_tag_id
            FROM key_rfid_assignments
            WHERE key_id = @keyId
              AND assigned_to IS NULL
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@keyId", keyId);

        var result = await command.ExecuteScalarAsync();

        return result is null || result == DBNull.Value
            ? null
            : Convert.ToUInt32(result);
    }

    private static object ToDbValue(uint? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object ToDbValue(DateTime? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object NormalizeFilter(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}