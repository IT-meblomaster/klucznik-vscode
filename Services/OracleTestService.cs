using Microsoft.Extensions.Configuration;
using Klucznik.Models;
using Npgsql;

namespace Klucznik.Services;

public class OracleTestService
{
    private readonly string _connectionString;

    public OracleTestService()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        _connectionString = config.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException(
                "Brak ConnectionStrings:PostgreSql w appsettings.json");
    }

    public async Task<PersonResult?> FindPersonByCardAsync(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return null;

        var rawCardNumber = cardNumber.Trim();
        var normalizedCardNumber = rawCardNumber.TrimStart('0');

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT
                nr_karty::text,
                imie,
                nazwisko
            FROM public.users_saik
            WHERE TRIM(nr_karty::text) = @rawCardNumber
               OR LTRIM(TRIM(nr_karty::text), '0') = @normalizedCardNumber
            ORDER BY
                CASE
                    WHEN TRIM(nr_karty::text) = @rawCardNumber THEN 0
                    ELSE 1
                END
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("rawCardNumber", rawCardNumber);
        command.Parameters.AddWithValue("normalizedCardNumber", normalizedCardNumber);

        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new PersonResult
            {
                CardNumber = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                FirstName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                LastName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            };
        }

        return null;
    }
}
