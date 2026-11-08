using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class LedgerPostingTests(V01Fixture fixture) : IClassFixture<V01Fixture>
{
    [Fact]
    public async Task CapturePostsOnceAndReplayPreservesDecision()
    {
        var customer = await fixture.SeedCustomerAsync();
        var payment = Guid.NewGuid();
        await fixture.AuthorizeAsync(customer, payment);
        var operation = Guid.NewGuid();
        var request = fixture.Capture(customer, payment, operation, 137);
        var replies = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => fixture.Ledger.PostAsJsonAsync("/internal/v1/operations", request)));
        try
        {
            var decisions = await Task.WhenAll(replies.Select(async reply =>
            {
                reply.EnsureSuccessStatusCode();
                return await reply.Content.ReadFromJsonAsync<JsonElement>();
            }));
            var journal = decisions[0].GetProperty("journalId").GetGuid();
            Assert.All(decisions, decision => Assert.Equal(journal, decision.GetProperty("journalId").GetGuid()));
            Assert.Equal(137L, await fixture.AvailableAsync(customer));
            Assert.Equal(2, await fixture.JournalLineCountAsync(journal));
            await fixture.RestrictAsync(customer);
            using var replay = await fixture.Ledger.PostAsJsonAsync("/internal/v1/operations", request);
            replay.EnsureSuccessStatusCode();
            var stored = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(journal, stored.GetProperty("journalId").GetGuid());
            Assert.Equal("CustomerRialAvailable", stored.GetProperty("decision").GetProperty("creditedBucket").GetString());
            Assert.Equal(0L, await fixture.RestrictedAsync(customer));
            using var changed = await fixture.Ledger.PostAsJsonAsync("/internal/v1/operations",
                fixture.Capture(customer, payment, operation, 138));
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            using var secondIdentity = await fixture.Ledger.PostAsJsonAsync("/internal/v1/operations",
                fixture.Capture(customer, payment, Guid.NewGuid(), 137));
            Assert.Equal(HttpStatusCode.Conflict, secondIdentity.StatusCode);
            using var denied = await fixture.Untrusted.GetAsync($"/internal/v1/operations/{operation}");
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            await fixture.AssertProjectionGuardAsync(customer);
        }
        finally
        {
            foreach (var reply in replies) reply.Dispose();
        }
    }
}
