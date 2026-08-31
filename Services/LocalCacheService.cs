using System;
using System.Collections.Generic;
using System.IO;
using Klucznik.Models;
using Microsoft.Data.Sqlite;

namespace Klucznik.Services;

public class LocalCacheService
{
    private readonly string _connectionString;

    public LocalCacheService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Klucznik");

        Directory.CreateDirectory(folder);

        var dbPath = Path.Combine(folder, "local_cache.db");
        _connectionString = $"Data Source={dbPath}";

        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            CREATE TABLE IF NOT EXISTS key_cache (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                building_id INTEGER NOT NULL,
                building TEXT,
                hanger TEXT,
                description TEXT,
                rfid_tag TEXT,
                current_rfid_tag_id INTEGER,
                is_active INTEGER NOT NULL,
                is_issued INTEGER NOT NULL,
                issued_to_name TEXT,
                issued_at TEXT
            );

            CREATE TABLE IF NOT EXISTS pending_events (
                id TEXT PRIMARY KEY,
                key_id INTEGER NOT NULL,
                key_name TEXT NOT NULL,
                key_building TEXT,
                rfid_tag_id INTEGER,
                action TEXT NOT NULL,
                person_card TEXT NOT NULL,
                person_first_name TEXT NOT NULL,
                person_last_name TEXT NOT NULL,
                person_offline INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0,
                sync_conflict INTEGER NOT NULL DEFAULT 0,
                retry_count INTEGER NOT NULL DEFAULT 0,
                last_attempt TEXT,
                last_error TEXT
            );
            """;

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    // ---- Cache kluczy (do offline'owego GetKeyByRfid) ----------------

    public void ReplaceKeysSnapshot(IEnumerable<KeyItem> keys)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using (var delete = new SqliteCommand("DELETE FROM key_cache;", connection, transaction))
            delete.ExecuteNonQuery();

        const string insertSql = """
            INSERT INTO key_cache
                (id, name, building_id, building, hanger, description, rfid_tag,
                 current_rfid_tag_id, is_active, is_issued, issued_to_name, issued_at)
            VALUES
                (@id, @name, @buildingId, @building, @hanger, @description, @rfidTag,
                 @currentRfidTagId, @isActive, @isIssued, @issuedToName, @issuedAt);
            """;

        foreach (var key in keys)
        {
            using var insert = new SqliteCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue("@id", key.Id);
            insert.Parameters.AddWithValue("@name", key.Name);
            insert.Parameters.AddWithValue("@buildingId", key.BuildingId);
            insert.Parameters.AddWithValue("@building", (object?)key.Building ?? DBNull.Value);
            insert.Parameters.AddWithValue("@hanger", (object?)key.Hanger ?? DBNull.Value);
            insert.Parameters.AddWithValue("@description", (object?)key.Description ?? DBNull.Value);
            insert.Parameters.AddWithValue("@rfidTag", (object?)key.RfidTag ?? DBNull.Value);
            insert.Parameters.AddWithValue("@currentRfidTagId", (object?)key.CurrentRfidTagId ?? DBNull.Value);
            insert.Parameters.AddWithValue("@isActive", key.IsActive ? 1 : 0);
            insert.Parameters.AddWithValue("@isIssued", key.IsIssued ? 1 : 0);
            insert.Parameters.AddWithValue("@issuedToName", (object?)key.IssuedToName ?? DBNull.Value);
            insert.Parameters.AddWithValue("@issuedAt", key.IssuedAt.HasValue
                ? key.IssuedAt.Value.ToString("o")
                : (object)DBNull.Value);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public KeyItem? FindKeyByRfid(string rfidTag)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT id, name, building_id, building, hanger, description, rfid_tag,
                   current_rfid_tag_id, is_active, is_issued, issued_to_name, issued_at
            FROM key_cache
            WHERE rfid_tag = @rfidTag
              AND is_active = 1
            LIMIT 1;
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@rfidTag", rfidTag.Trim());

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadKeyItem(reader) : null;
    }

    public void ApplyLocalToggle(int keyId, bool nowIssued, string? issuedToName, DateTime? issuedAt)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            UPDATE key_cache
            SET is_issued = @isIssued,
                issued_to_name = @issuedToName,
                issued_at = @issuedAt
            WHERE id = @keyId;
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@isIssued", nowIssued ? 1 : 0);
        command.Parameters.AddWithValue("@issuedToName", (object?)issuedToName ?? DBNull.Value);
        command.Parameters.AddWithValue("@issuedAt", issuedAt.HasValue ? issuedAt.Value.ToString("o") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@keyId", keyId);
        command.ExecuteNonQuery();
    }

    private static KeyItem ReadKeyItem(SqliteDataReader reader)
    {
        return new KeyItem
        {
            Id = (uint)reader.GetInt64(0),
            Name = reader.GetString(1),
            BuildingId = (uint)reader.GetInt64(2),
            Building = reader.IsDBNull(3) ? null : reader.GetString(3),
            Hanger = reader.IsDBNull(4) ? null : reader.GetString(4),
            Description = reader.IsDBNull(5) ? null : reader.GetString(5),
            RfidTag = reader.IsDBNull(6) ? null : reader.GetString(6),
            CurrentRfidTagId = reader.IsDBNull(7) ? null : (uint)reader.GetInt64(7),
            IsActive = reader.GetInt64(8) == 1,
            IsIssued = reader.GetInt64(9) == 1,
            IssuedToName = reader.IsDBNull(10) ? null : reader.GetString(10),
            IssuedAt = reader.IsDBNull(11) ? null : DateTime.Parse(reader.GetString(11))
        };
    }

    // ---- Kolejka zdarzeń offline --------------------------------------

    public void EnqueueEvent(PendingKeyEvent ev)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO pending_events
                (id, key_id, key_name, key_building, rfid_tag_id, action,
                 person_card, person_first_name, person_last_name, person_offline,
                 created_at, synced, sync_conflict, retry_count)
            VALUES
                (@id, @keyId, @keyName, @keyBuilding, @rfidTagId, @action,
                 @personCard, @personFirstName, @personLastName, @personOffline,
                 @createdAt, 0, 0, 0);
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", ev.Id.ToString());
        command.Parameters.AddWithValue("@keyId", ev.KeyId);
        command.Parameters.AddWithValue("@keyName", ev.KeyName);
        command.Parameters.AddWithValue("@keyBuilding", (object?)ev.KeyBuilding ?? DBNull.Value);
        command.Parameters.AddWithValue("@rfidTagId", (object?)ev.RfidTagId ?? DBNull.Value);
        command.Parameters.AddWithValue("@action", ev.Action);
        command.Parameters.AddWithValue("@personCard", ev.PersonCard);
        command.Parameters.AddWithValue("@personFirstName", ev.PersonFirstName);
        command.Parameters.AddWithValue("@personLastName", ev.PersonLastName);
        command.Parameters.AddWithValue("@personOffline", ev.PersonOffline ? 1 : 0);
        command.Parameters.AddWithValue("@createdAt", ev.CreatedAt.ToString("o"));
        command.ExecuteNonQuery();
    }

    public List<PendingKeyEvent> GetUnsyncedEvents()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT id, key_id, key_name, key_building, rfid_tag_id, action,
                   person_card, person_first_name, person_last_name, person_offline,
                   created_at, retry_count, sync_conflict
            FROM pending_events
            WHERE synced = 0
            ORDER BY created_at;
            """;

        using var command = new SqliteCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var result = new List<PendingKeyEvent>();

        while (reader.Read())
        {
            result.Add(new PendingKeyEvent
            {
                Id = Guid.Parse(reader.GetString(0)),
                KeyId = (uint)reader.GetInt64(1),
                KeyName = reader.GetString(2),
                KeyBuilding = reader.IsDBNull(3) ? null : reader.GetString(3),
                RfidTagId = reader.IsDBNull(4) ? null : (uint)reader.GetInt64(4),
                Action = reader.GetString(5),
                PersonCard = reader.GetString(6),
                PersonFirstName = reader.GetString(7),
                PersonLastName = reader.GetString(8),
                PersonOffline = reader.GetInt64(9) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                RetryCount = (int)reader.GetInt64(11),
                SyncConflict = reader.GetInt64(12) == 1
            });
        }

        return result;
    }

    public int CountUnsyncedEvents()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = new SqliteCommand(
            "SELECT COUNT(*) FROM pending_events WHERE synced = 0;", connection);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void MarkSynced(Guid id, bool conflict = false)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            UPDATE pending_events
            SET synced = 1, sync_conflict = @conflict, last_attempt = @now
            WHERE id = @id;
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@conflict", conflict ? 1 : 0);
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
        command.Parameters.AddWithValue("@id", id.ToString());
        command.ExecuteNonQuery();
    }

    public void MarkFailedAttempt(Guid id, string error)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            UPDATE pending_events
            SET retry_count = retry_count + 1,
                last_attempt = @now,
                last_error = @error
            WHERE id = @id;
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("o"));
        command.Parameters.AddWithValue("@error", error);
        command.Parameters.AddWithValue("@id", id.ToString());
        command.ExecuteNonQuery();
    }

    public void UpdatePersonNameIfOffline(Guid id, string firstName, string lastName)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
            UPDATE pending_events
            SET person_first_name = @firstName,
                person_last_name = @lastName,
                person_offline = 0
            WHERE id = @id;
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@firstName", firstName);
        command.Parameters.AddWithValue("@lastName", lastName);
        command.Parameters.AddWithValue("@id", id.ToString());
        command.ExecuteNonQuery();
    }
}