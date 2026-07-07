using Microsoft.Extensions.Configuration;
using Klucznik.Models;
using Oracle.ManagedDataAccess.Client;

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

        _connectionString = config.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException("Brak ConnectionStrings:Oracle w appsettings.json");
    }

    public async Task<PersonResult?> FindPersonByCardAsync(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return null;
        }

        var normalizedCardNumber = cardNumber.Trim().TrimStart('0');

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT
                NR_EWIDENCYJNY,
                IMIE_1,
                NAZWISKO
            FROM MEBLO_MP_OSOBY
            WHERE NUMER_KARTY_RCP = :cardNumber";

        await using var command = new OracleCommand(sql, connection);
        command.BindByName = true;
        command.Parameters.Add(new OracleParameter("cardNumber", normalizedCardNumber));

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
