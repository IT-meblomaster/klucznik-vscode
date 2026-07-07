using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Klucznik.Models;

namespace Klucznik.Services;

public class DatabaseSettingsService
{
    private readonly string _settingsPath;

    public DatabaseSettingsService()
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public (DbSettingsSection Oracle, DbSettingsSection MySql) Load()
    {
        var root = LoadJsonObject();
        var connectionStrings = root["ConnectionStrings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak sekcji ConnectionStrings w appsettings.json.");

        var oracleRaw = connectionStrings["Oracle"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Brak wpisu ConnectionStrings:Oracle.");

        var mariaDbRaw = connectionStrings["MariaDb"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Brak wpisu ConnectionStrings:MariaDb.");

        return (ParseOracle(oracleRaw), ParseMariaDb(mariaDbRaw));
    }

    public ScannerSettingsSection LoadScannerSettings()
    {
        var root = LoadJsonObject();
        var scanner = root["Scanner"]?.AsObject();

        if (scanner is null)
        {
            return new ScannerSettingsSection();
        }

        var vid = scanner["Vid"]?.GetValue<string>() ?? "VID_08FF";
        var pid = scanner["Pid"]?.GetValue<string>() ?? "PID_0009";
        var sid = scanner["Sid"]?.GetValue<string>() ?? string.Empty;

        return new ScannerSettingsSection
        {
            Vid = NormalizeVidPid(vid, "VID_"),
            Pid = NormalizeVidPid(pid, "PID_"),
            Sid = sid.Trim().ToUpperInvariant()
        };
    }

    public void Save(DbSettingsSection section)
    {
        var root = LoadJsonObject();
        var connectionStrings = root["ConnectionStrings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak sekcji ConnectionStrings w appsettings.json.");

        if (string.Equals(section.ConfigKey, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            connectionStrings["Oracle"] = BuildOracleConnectionString(section);
        }
        else if (string.Equals(section.ConfigKey, "MariaDb", StringComparison.OrdinalIgnoreCase))
        {
            connectionStrings["MariaDb"] = BuildMariaDbConnectionString(section);
        }
        else
        {
            throw new InvalidOperationException($"Nieobsługiwany klucz konfiguracji: {section.ConfigKey}");
        }

        SaveJsonObject(root);
    }

    public void SaveScannerSettings(ScannerSettingsSection settings)
    {
        var root = LoadJsonObject();

        var scanner = root["Scanner"]?.AsObject();

        if (scanner is null)
        {
            scanner = new JsonObject();
            root["Scanner"] = scanner;
        }

        scanner["Vid"] = NormalizeVidPid(settings.Vid, "VID_");
        scanner["Pid"] = NormalizeVidPid(settings.Pid, "PID_");
        scanner["Sid"] = (settings.Sid ?? string.Empty).Trim().ToUpperInvariant();

        SaveJsonObject(root);
    }

    private JsonObject LoadJsonObject()
    {
        if (!File.Exists(_settingsPath))
            throw new FileNotFoundException("Nie znaleziono pliku appsettings.json.", _settingsPath);

        var json = File.ReadAllText(_settingsPath);
        var node = JsonNode.Parse(json)?.AsObject();

        if (node is null)
            throw new InvalidOperationException("Nie udało się odczytać pliku appsettings.json.");

        return node;
    }

    private void SaveJsonObject(JsonObject root)
    {
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsPath, json);
    }

    private static DbSettingsSection ParseOracle(string connectionString)
    {
        var user = ExtractBetween(connectionString, "User Id=", ";");
        var password = ExtractBetween(connectionString, "Password=", ";");
        var host = ExtractBetween(connectionString, "(HOST=", ")");
        var sid = ExtractBetween(connectionString, "(SID=", ")");

        return new DbSettingsSection
        {
            SectionName = "Oracle",
            ConfigKey = "Oracle",
            Address = host,
            User = user,
            Password = password,
            DatabaseName = sid,
            IsEditing = false
        };
    }

    private static DbSettingsSection ParseMariaDb(string connectionString)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);

        parts.TryGetValue("Server", out var server);
        parts.TryGetValue("User", out var user);
        parts.TryGetValue("Password", out var password);
        parts.TryGetValue("Database", out var database);

        return new DbSettingsSection
        {
            SectionName = "mySQL",
            ConfigKey = "MariaDb",
            Address = server ?? string.Empty,
            User = user ?? string.Empty,
            Password = password ?? string.Empty,
            DatabaseName = database ?? string.Empty,
            IsEditing = false
        };
    }

    private static string BuildOracleConnectionString(DbSettingsSection section)
    {
        return
            $"User Id={section.User};" +
            $"Password={section.Password};" +
            $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={section.Address})(PORT=1521))(CONNECT_DATA=(SID={section.DatabaseName})))";
    }

    private static string BuildMariaDbConnectionString(DbSettingsSection section)
    {
        return
            $"Server={section.Address};" +
            $"Database={section.DatabaseName};" +
            $"User={section.User};" +
            $"Password={section.Password};";
    }

    private static string ExtractBetween(string source, string startToken, string endToken)
    {
        var startIndex = source.IndexOf(startToken, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
            return string.Empty;

        startIndex += startToken.Length;

        var endIndex = source.IndexOf(endToken, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
            return source[startIndex..];

        return source[startIndex..endIndex];
    }

    private static string NormalizeVidPid(string value, string prefix)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
            return prefix == "VID_" ? "VID_08FF" : "PID_0009";

        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            normalized = prefix + normalized;

        return normalized;
    }
}
