using System;
using System.Security.Cryptography;
using System.Text.Json;
using Klucznik.Models;
using MySqlConnector;

namespace Klucznik.Services;

public class AdminPasswordService
{
    private const string AdminPasswordSettingKey = "admin_password";
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int DefaultIterations = 210_000;

    private readonly string _connectionString;

    public AdminPasswordService()
    {
        _connectionString = DatabaseConfig.Instance.MariaDbConnectionString;
    }

    public bool HasPassword()
    {
        var settings = Load();

        return !string.IsNullOrWhiteSpace(settings.Salt)
            && !string.IsNullOrWhiteSpace(settings.Hash);
    }

    public AdminPasswordSettings Load()
    {
        EnsureSettingsTableExists();

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT setting_value
            FROM app_settings
            WHERE setting_key = @settingKey
            LIMIT 1;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@settingKey", AdminPasswordSettingKey);

        var raw = command.ExecuteScalar() as string;

        if (string.IsNullOrWhiteSpace(raw))
            return new AdminPasswordSettings();

        var settings = JsonSerializer.Deserialize<AdminPasswordSettings>(raw);

        return settings ?? new AdminPasswordSettings();
    }

    public void SavePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Hasło nie moĹĽe byÄ‡ puste.");

        EnsureSettingsTableExists();

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = HashPassword(password, salt, DefaultIterations);

        var settings = new AdminPasswordSettings
        {
            Algorithm = "PBKDF2-SHA256",
            Iterations = DefaultIterations,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash)
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO app_settings (setting_key, setting_value)
            VALUES (@settingKey, @settingValue)
            ON DUPLICATE KEY UPDATE
                setting_value = VALUES(setting_value),
                updated_at = CURRENT_TIMESTAMP;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@settingKey", AdminPasswordSettingKey);
        command.Parameters.AddWithValue("@settingValue", json);

        command.ExecuteNonQuery();
    }

    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        var settings = Load();

        if (string.IsNullOrWhiteSpace(settings.Salt) || string.IsNullOrWhiteSpace(settings.Hash))
            return false;

        var salt = Convert.FromBase64String(settings.Salt);
        var expectedHash = Convert.FromBase64String(settings.Hash);
        var actualHash = HashPassword(password, salt, settings.Iterations);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private void EnsureSettingsTableExists()
    {
        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            CREATE TABLE IF NOT EXISTS app_settings (
                setting_key VARCHAR(100) NOT NULL PRIMARY KEY,
                setting_value LONGTEXT NOT NULL,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            );
            """;

        using var command = new MySqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSizeBytes);
    }
}
