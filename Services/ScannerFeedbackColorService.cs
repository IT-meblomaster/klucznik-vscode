using System;
using System.Text.Json;
using System.Windows.Media;
using Klucznik.Models;
using MySqlConnector;

namespace Klucznik.Services;

public class ScannerFeedbackColorService
{
    private const string SettingKey = "scanner_feedback_colors";

    private readonly string _connectionString;

    public ScannerFeedbackColorService()
    {
        var config = new DatabaseConfig();
        _connectionString = config.MariaDbConnectionString;
    }

    public ScannerFeedbackColorSettings Load()
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
        command.Parameters.AddWithValue("@settingKey", SettingKey);

        var raw = command.ExecuteScalar() as string;

        if (string.IsNullOrWhiteSpace(raw))
        {
            var defaults = new ScannerFeedbackColorSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var settings =
                JsonSerializer.Deserialize<ScannerFeedbackColorSettings>(raw)
                ?? new ScannerFeedbackColorSettings();

            Normalize(settings);

            return settings;
        }
        catch (JsonException)
        {
            // Uszkodzona wartość nie powinna uniemożliwić startu aplikacji.
            var defaults = new ScannerFeedbackColorSettings();
            Save(defaults);

            return defaults;
        }
    }

    public void Save(ScannerFeedbackColorSettings settings)
    {
        Normalize(settings);

        EnsureSettingsTableExists();

        var json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions
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

        command.Parameters.AddWithValue("@settingKey", SettingKey);
        command.Parameters.AddWithValue("@settingValue", json);

        command.ExecuteNonQuery();
    }

    public static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));

        brush.Freeze();

        return brush;
    }

    private static void Normalize(ScannerFeedbackColorSettings settings)
    {
        settings.IssuedBackground =
            NormalizeColor(
                settings.IssuedBackground,
                "#DCFCE7");

        settings.IssuedBorder =
            NormalizeColor(
                settings.IssuedBorder,
                "#86EFAC");

        settings.ReturnedBackground =
            NormalizeColor(
                settings.ReturnedBackground,
                "#F3E8FF");

        settings.ReturnedBorder =
            NormalizeColor(
                settings.ReturnedBorder,
                "#C084FC");
    }

    private static string NormalizeColor(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            _ = ColorConverter.ConvertFromString(value);

            return value
                .Trim()
                .ToUpperInvariant();
        }
        catch
        {
            return fallback;
        }
    }

    private void EnsureSettingsTableExists()
    {
        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            CREATE TABLE IF NOT EXISTS app_settings (
                setting_key VARCHAR(100) NOT NULL PRIMARY KEY,
                setting_value LONGTEXT NOT NULL,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    ON UPDATE CURRENT_TIMESTAMP
            );
            """;

        using var command = new MySqlCommand(sql, connection);

        command.ExecuteNonQuery();
    }
}