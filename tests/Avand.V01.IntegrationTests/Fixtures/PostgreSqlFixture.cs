using Testcontainers.PostgreSql;
using Xunit;

namespace Avand.V01.IntegrationTests.Fixtures;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string? external = Environment.GetEnvironmentVariable("AVAND_V01_TEST_POSTGRES");
    private PostgreSqlContainer? container;

    public string AdminConnectionString => external ?? container!.GetConnectionString();

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(external))
            return Task.CompletedTask;

        container = new PostgreSqlBuilder("postgres:17.6").Build();
        return container.StartAsync();
    }

    public Task DisposeAsync()
    {
        // The next class's container can reuse this host port, giving an identical connection string. Drop pooled
        // sessions to this server so they are never handed to the next fixture.
        Npgsql.NpgsqlConnection.ClearAllPools();
        return container?.DisposeAsync().AsTask() ?? Task.CompletedTask;
    }
}
