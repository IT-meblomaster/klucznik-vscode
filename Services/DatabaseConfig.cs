using Microsoft.Extensions.Configuration;

namespace Klucznik.Services;

public class DatabaseConfig
{
    public string OracleConnectionString { get; }
    public string MariaDbConnectionString { get; }

    public DatabaseConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        OracleConnectionString = config.GetConnectionString("Oracle")
            ?? throw new Exception("Brak Oracle connection string");

        MariaDbConnectionString = config.GetConnectionString("MariaDb")
            ?? throw new Exception("Brak MariaDb connection string");
    }
}
