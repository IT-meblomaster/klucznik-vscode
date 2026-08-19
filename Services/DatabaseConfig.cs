using Microsoft.Extensions.Configuration;

namespace Klucznik.Services;

public class DatabaseConfig
{
    public string PostgreSqlConnectionString { get; }
    public string MariaDbConnectionString { get; }

    public DatabaseConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        PostgreSqlConnectionString = config.GetConnectionString("PostgreSql")
            ?? throw new Exception("Brak PostgreSql connection string");

        MariaDbConnectionString = config.GetConnectionString("MariaDb")
            ?? throw new Exception("Brak MariaDb connection string");
    }
}
