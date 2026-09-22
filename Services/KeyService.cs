using Klucznik.Models;
using MySqlConnector;

namespace Klucznik.Services;

public class KeyService
{
    private readonly string _connectionString;

    public KeyService()
    {
        _connectionString = DatabaseConfig.Instance.MariaDbConnectionString;
    }
    public async Task<List<BuildingItem>> GetBuildingsAsync()
    {
        var result = new List<BuildingItem>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT id, name
            FROM buildings
            WHERE is_active = 1
            ORDER BY name;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new BuildingItem
            {
                Id = reader.GetFieldValue<uint>(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<uint> InsertBuildingAsync(string name)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO buildings (name, is_active)
            VALUES (@name, 1);
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name.Trim());

        await command.ExecuteNonQueryAsync();
        return (uint)command.LastInsertedId;
    }

    public async Task UpdateBuildingAsync(uint id, string name)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            UPDATE buildings
            SET name = @name
            WHERE id = @id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", name.Trim());

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteBuildingAsync(uint id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            UPDATE buildings
            SET is_active = 0
            WHERE id = @id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
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
                k.building_id,
                b.name AS building,
                k.zawieszka,
                k.description,
                r.rfid_code,
                a.rfid_tag_id,
                k.is_active,
                CASE WHEN kl.id IS NOT NULL THEN 1 ELSE 0 END AS is_issued,
                kl.issued_to_name,
                kl.issued_at
            FROM `keys` k
            JOIN buildings b
                ON b.id = k.building_id
            LEFT JOIN key_rfid_assignments a
                ON a.key_id = k.id
               AND a.assigned_to IS NULL
            LEFT JOIN rfid_tags r
                ON r.id = a.rfid_tag_id
            LEFT JOIN key_loans kl
                ON kl.key_id = k.id
               AND kl.returned_at IS NULL
            WHERE k.is_active = 1
            ORDER BY b.name, k.name;
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
                k.building_id,
                b.name AS building,
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
            JOIN buildings b
                ON b.id = k.building_id
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
                    b.name AS building,
                    kl.issued_to_name AS user_name,
                    kl.issued_to_card AS user_card,
                    r.rfid_code AS rfid_code
                FROM key_loans kl
                JOIN `keys` k ON k.id = kl.key_id
                JOIN buildings b ON b.id = k.building_id
                LEFT JOIN rfid_tags r ON r.id = kl.rfid_tag_id

                UNION ALL

                SELECT
                    kl.returned_at AS event_time,
                    'Zwrot' AS event_type,
                    k.name AS key_name,
                    b.name AS building,
                    kl.returned_by_name AS user_name,
                    kl.returned_by_card AS user_card,
                    r.rfid_code AS rfid_code
                FROM key_loans kl
                JOIN `keys` k ON k.id = kl.key_id
                JOIN buildings b ON b.id = k.building_id
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

    public async Task<uint> InsertAsync(string name, uint buildingId, string? hanger, string? description)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                INSERT INTO `keys` (name, building_id, zawieszka, description, is_active)
                VALUES (@name, @buildingId, @hanger, @description, 1);
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@buildingId", buildingId);
            command.Parameters.AddWithValue("@hanger", NormalizeRequiredText(hanger));
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

    public async Task UpdateAsync(uint id, string name, uint buildingId, string? hanger, string? description, bool removeRfid)
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
                    building_id = @buildingId,
                    zawieszka = @hanger,
                    description = @description
                WHERE id = @id;
                """;

            await using var command = new MySqlCommand(sql, connection, (MySqlTransaction)transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@buildingId", buildingId);
            command.Parameters.AddWithValue("@hanger", NormalizeRequiredText(hanger));
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

    public async Task<KeyAccessSnapshot> GetAccessSnapshotAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        // Jedno zapytanie = spójny obraz flag i list kart.
        await using var command = new MySqlCommand("""
            SELECT k.id, k.is_restricted, c.card_number
            FROM `keys` k LEFT JOIN key_authorized_cards c ON c.key_id=k.id
            WHERE k.is_active=1 ORDER BY k.id, c.card_number;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var snapshot = new KeyAccessSnapshot();
        KeyAccessPolicy? policy = null;
        while (await reader.ReadAsync())
        {
            var id = reader.GetFieldValue<uint>(0);
            if (policy is null || policy.KeyId != id)
            {
                policy = new KeyAccessPolicy { KeyId = id, IsRestricted = reader.GetBoolean(1) };
                snapshot.Policies.Add(policy);
            }
            if (!reader.IsDBNull(2)) policy.Cards.Add(CardNumber.Normalize(reader.GetString(2)));
        }
        return snapshot;
    }

    public async Task<KeyLoanOperationResult> RegisterIssueOrReturnAsync(
        KeyItem key, PersonResult person, Guid eventId, bool expectedIssue,
        DateTime eventTime, bool replay = false, DateTime? offlinePolicyAt = null,
        bool offlineRestricted = false, bool dependentConflict = false)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // Blokada istniejącego klucza serializuje wydania również przy pustym key_loans.
        await using var keyCommand = new MySqlCommand(
            "SELECT is_restricted, is_active FROM `keys` WHERE id=@key FOR UPDATE;", connection, transaction);
        keyCommand.Parameters.AddWithValue("@key", key.Id);
        bool restricted, active;
        await using (var reader = await keyCommand.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) throw new KeyAccessDeniedException("Klucz nie istnieje.");
            restricted = reader.GetBoolean(0);
            active = reader.GetBoolean(1);
        }

        // Ponowienie po zerwanym połączeniu/niepewnym COMMIT nie powiela operacji.
        await using var previous = new MySqlCommand(
            "SELECT action, is_restricted, has_conflict, message FROM key_client_events WHERE event_id=@id;", connection, transaction);
        previous.Parameters.AddWithValue("@id", eventId.ToString());
        await using (var reader = await previous.ExecuteReaderAsync())
            if (await reader.ReadAsync())
                return new KeyLoanOperationResult {
                    IsIssue = !reader.GetBoolean(2) && reader.GetString(0) == "ISSUE",
                    IsReturn = !reader.GetBoolean(2) && reader.GetString(0) == "RETURN",
                    IsRestricted = reader.GetBoolean(1), HasConflict = reader.GetBoolean(2),
                    Message = reader.GetString(3)
                };

        ulong? loanId = null;
        string? issuedName = null;
        DateTime? issuedAt = null;
        await using var open = new MySqlCommand("""
            SELECT id, issued_to_name, issued_at FROM key_loans
            WHERE key_id=@key AND returned_at IS NULL LIMIT 1 FOR UPDATE;
            """, connection, transaction);
        open.Parameters.AddWithValue("@key", key.Id);
        await using (var reader = await open.ExecuteReaderAsync())
            if (await reader.ReadAsync())
            {
                loanId = reader.GetFieldValue<ulong>(0);
                issuedName = reader.GetString(1);
                issuedAt = reader.GetDateTime(2);
            }

        string? conflict = null;
        if (expectedIssue && loanId.HasValue) conflict = "Klucz ma już otwarte wypożyczenie.";
        if (!expectedIssue && !loanId.HasValue) conflict = "Klucz nie ma otwartego wypożyczenia.";
        if (!expectedIssue && issuedAt.HasValue && eventTime < issuedAt.Value)
            conflict = "Zwrot jest starszy niż aktualne wypożyczenie.";
        if (expectedIssue && !active) conflict = "Klucz jest nieaktywny.";
        if (dependentConflict) conflict = "Wcześniejsze zaległe zdarzenie tego klucza ma konflikt; wymagana kontrola historii.";
        if (conflict is not null && !replay)
            throw new KeyAccessDeniedException(conflict + " Zeskanuj klucz ponownie.");

        // Zaległe zdarzenie jest faktem historycznym: zapisujemy decyzję podjętą
        // według lokalnej kopii, a nie udzielamy nowej zgody według dzisiejszej listy.
        // Stare zdarzenia bez tej informacji są sprawdzane według bieżącej bazy.
        bool useOfflineDecision = replay && offlinePolicyAt.HasValue;
        bool eventRestricted = useOfflineDecision ? offlineRestricted : restricted;
        if (expectedIssue && restricted && !useOfflineDecision && conflict is null)
        {
            var card = CardNumber.Normalize(person.CardNumber);
            await using var permission = new MySqlCommand("""
                SELECT card_number FROM key_authorized_cards WHERE key_id=@key;
                """, connection, transaction);
            permission.Parameters.AddWithValue("@key", key.Id);
            bool allowed = false;
            await using (var reader = await permission.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    allowed |= CardNumber.Normalize(reader.GetString(0)) == card;
            if (!allowed)
            {
                if (!replay) throw new KeyAccessDeniedException("Brak uprawnienia do pobrania tego klucza.");
                conflict = "Zdarzenie sprzed aktualizacji: brak potwierdzonego uprawnienia.";
            }
        }

        var rfidId = await GetActiveRfidTagIdInternalAsync(connection, transaction, key.Id);
        string name = $"{person.FirstName} {person.LastName}".Trim();
        string message;
        if (conflict is not null)
            message = $"KONFLIKT synchronizacji: {key.KeyWithBuildingDisplay}, karta {person.CardNumber}: {conflict}";
        else if (expectedIssue)
        {
            await using var issue = new MySqlCommand("""
                INSERT INTO key_loans(key_id,rfid_tag_id,issued_to_card,issued_to_name,issued_at)
                VALUES(@key,@rfid,@card,@name,@at);
                """, connection, transaction);
            issue.Parameters.AddWithValue("@key", key.Id);
            issue.Parameters.AddWithValue("@rfid", ToDbValue(rfidId));
            issue.Parameters.AddWithValue("@card", person.CardNumber);
            issue.Parameters.AddWithValue("@name", name);
            issue.Parameters.AddWithValue("@at", eventTime);
            await issue.ExecuteNonQueryAsync();
            message = $"Wydano klucz: {key.KeyWithBuildingDisplay} -> {name}";
        }
        else
        {
            await using var returned = new MySqlCommand("""
                UPDATE key_loans SET returned_by_card=@card, returned_by_name=@name,
                    returned_at=@at WHERE id=@id;
                """, connection, transaction);
            returned.Parameters.AddWithValue("@card", person.CardNumber);
            returned.Parameters.AddWithValue("@name", name);
            returned.Parameters.AddWithValue("@at", eventTime);
            returned.Parameters.AddWithValue("@id", loanId!.Value);
            await returned.ExecuteNonQueryAsync();
            message = $"Zwrócono klucz: {key.KeyWithBuildingDisplay} <- {name} (pobrał: {issuedName})";
        }
        if (expectedIssue && eventRestricted) message += " [klucz specjalny]";
        var details = message + (replay ? $" [offline; zdarzenie: {eventTime:yyyy-MM-dd HH:mm:ss}; kopia uprawnień UTC: {offlinePolicyAt:O}]" : "");
        await InsertLogAsync(connection, transaction, key.Id, rfidId,
            conflict is not null ? "SYNC_CONFLICT" : expectedIssue ? "ISSUE" : "RETURN",
            details.Length <= 1000 ? details : details[..1000]);
        await using var receipt = new MySqlCommand("""
            INSERT INTO key_client_events(event_id,key_id,action,card_number,event_at,is_offline,
                policy_synced_at,is_restricted,has_conflict,message)
            VALUES(@id,@key,@action,@card,@at,@offline,@policy,@restricted,@conflict,@message);
            """, connection, transaction);
        receipt.Parameters.AddWithValue("@id", eventId.ToString());
        receipt.Parameters.AddWithValue("@key", key.Id);
        receipt.Parameters.AddWithValue("@action", expectedIssue ? "ISSUE" : "RETURN");
        receipt.Parameters.AddWithValue("@card", person.CardNumber);
        receipt.Parameters.AddWithValue("@at", eventTime);
        receipt.Parameters.AddWithValue("@offline", replay);
        receipt.Parameters.AddWithValue("@policy", ToDbValue(offlinePolicyAt));
        receipt.Parameters.AddWithValue("@restricted", eventRestricted);
        receipt.Parameters.AddWithValue("@conflict", conflict is not null);
        receipt.Parameters.AddWithValue("@message", message);
        await receipt.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return new KeyLoanOperationResult {
            IsIssue = conflict is null && expectedIssue, IsReturn = conflict is null && !expectedIssue,
            IsRestricted = eventRestricted, HasConflict = conflict is not null, Message = message
        };
    }

    private static KeyItem ReadKeyItem(MySqlDataReader reader)
    {
        return new KeyItem
        {
            Id = reader.GetFieldValue<uint>(0),
            Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            BuildingId = reader.GetFieldValue<uint>(2),
            Building = reader.IsDBNull(3) ? null : reader.GetString(3),
            Hanger = reader.IsDBNull(4) ? null : reader.GetString(4),
            Description = reader.IsDBNull(5) ? null : reader.GetString(5),
            RfidTag = reader.IsDBNull(6) ? null : reader.GetString(6),
            CurrentRfidTagId = reader.IsDBNull(7) ? null : reader.GetFieldValue<uint>(7),
            IsActive = !reader.IsDBNull(8) && reader.GetBoolean(8),
            IsIssued = !reader.IsDBNull(9) && reader.GetBoolean(9),
            IssuedToName = reader.IsDBNull(10) ? null : reader.GetString(10),
            IssuedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
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

    private static string NormalizeRequiredText(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
