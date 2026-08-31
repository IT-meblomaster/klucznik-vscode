using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Klucznik.Models;
using Npgsql;

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

        var postgreSqlRaw = connectionStrings["PostgreSql"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Brak wpisu ConnectionStrings:PostgreSql.");

        var mariaDbRaw = connectionStrings["MariaDb"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Brak wpisu ConnectionStrings:MariaDb.");

        return (ParsePostgreSql(postgreSqlRaw), ParseMariaDb(mariaDbRaw));
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

        return new ScannerSettingsSection
        {
            Vid = NormalizeVidPid(vid, "VID_"),
            Pid = NormalizeVidPid(pid, "PID_")
        };
    }

    public void Save(DbSettingsSection section)
    {
        var root = LoadJsonObject();
        var connectionStrings = root["ConnectionStrings"]?.AsObject()
            ?? throw new InvalidOperationException("Brak sekcji ConnectionStrings w appsettings.json.");

        if (string.Equals(section.ConfigKey, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            connectionStrings["PostgreSql"] = BuildPostgreSqlConnectionString(section);
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

    private static DbSettingsSection ParsePostgreSql(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        return new DbSettingsSection
        {
            SectionName = "PostgreSQL",
            ConfigKey = "PostgreSql",
            Address = builder.Host ?? string.Empty,
            Port = builder.Port,
            User = builder.Username ?? string.Empty,
            Password = builder.Password ?? string.Empty,
            DatabaseName = builder.Database ?? string.Empty,
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
            Port = 0,
            User = user ?? string.Empty,
            Password = password ?? string.Empty,
            DatabaseName = database ?? string.Empty,
            IsEditing = false
        };
    }

    private static string BuildPostgreSqlConnectionString(DbSettingsSection section)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = section.Address,
            Port = section.Port > 0 ? section.Port : 5432,
            Database = section.DatabaseName,
            Username = section.User,
            Password = section.Password
        };

        return builder.ConnectionString;
    }

    private static string BuildMariaDbConnectionString(DbSettingsSection section)
    {
        return
            $"Server={section.Address};" +
            $"Database={section.DatabaseName};" +
            $"User={section.User};" +
            $"Password={section.Password};";
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
