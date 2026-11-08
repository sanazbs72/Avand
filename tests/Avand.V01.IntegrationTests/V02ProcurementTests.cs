using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>AC-15 (Application fault after the Ledger commit) and AC-16 (procurement intent equals the increase in
/// the backing deficit caused by each posted purchase, exactly once).</summary>
public sealed class V02ProcurementTests(V02Fixture fixture) : IClassFixture<V02Fixture>
{
    [Fact]
    public async Task EachPostedPurchaseRecordsOnlyItsOwnDeficitIncreaseOnce()
    {
        await fixture.FundAsync(1_030_000_000);
        fixture.ObservePrice(1_000_000, 999_000); // 1 mg = 1,000 IRR
        await fixture.SetBackingAsync(false);
        var opening = await BuyAsync(975_000, "Completed");
        Assert.Equal(0, await fixture.ProcurementCountAsync(Id(opening)));

        // Outage, 980,000 mg remaining confirmed custody and 975,000 mg claims: a covered 5,000 mg buy.
        await fixture.SetBackingAsync(true, 20_000);
        var covered = await BuyAsync(5_000, "Completed");
        Assert.Equal(0, await fixture.ProcurementCountAsync(Id(covered)));

        // Same initial surplus of 5,000 mg (985,000 remaining, 980,000 claims): 6,000 mg creates a 1,000 mg deficit.
        // The Application fails to apply the committed result once, then recovers it.
        await fixture.SetBackingAsync(true, 15_000);
        await fixture.ArmApplicationApplyFailureAsync();
        Guid initialId;
        try
        {
            var quote = await fixture.QuoteAsync(6_000_000);
            initialId = await fixture.ConfirmedOrderAsync(quote.GetProperty("quoteId").GetGuid());
            await V01Fixture.EventuallyAsync(async () => await fixture.PostedBuyCountAsync(initialId) == 1);
            await V01Fixture.EventuallyAsync(async () => await fixture.ReadApplicationAsync(
                """SELECT "Attempts"::bigint FROM "BuyRecoveryWork" WHERE "OrderId" = $1""", initialId) >= 2);
            var pending = await fixture.Customer.GetFromJsonAsync<JsonElement>($"/api/v1/gold/buy-orders/{initialId}");
            Assert.Equal("Processing", pending.GetProperty("status").GetString());
            Assert.Equal(0, await fixture.ProcurementCountAsync(initialId));
        }
        finally
        {
            await fixture.DisarmApplicationApplyFailureAsync();
        }
        await fixture.AwaitBuyAsync(initialId, "Completed");
        Assert.Equal(1, await fixture.PostedBuyCountAsync(initialId));
        Assert.Equal(1, await fixture.ProcurementCountAsync(initialId));
        Assert.Equal(1_000, await fixture.ProcurementMgAsync(initialId));

        // A further 2,000 mg buy increases the deficit from 1,000 to 3,000 mg.
        var incremental = await BuyAsync(2_000, "Completed");
        Assert.Equal(1, await fixture.ProcurementCountAsync(Id(incremental)));
        Assert.Equal(2_000, await fixture.ProcurementMgAsync(Id(incremental)));

        // A rejected purchase records nothing.
        var rejected = await BuyAsync(20_000, "Rejected");
        Assert.Equal("InsufficientBacking", rejected.GetProperty("reasonCode").GetString());
        Assert.Equal(0, await fixture.ProcurementCountAsync(Id(rejected)));

        // Replays and an Application restart change neither the count nor the quantities.
        using var trading = fixture.TradingClient();
        using (var replay = await trading.PostAsync("/internal/v1/operations",
            new StringContent(await fixture.FrozenRequestAsync(initialId), Encoding.UTF8, "application/json")))
        {
            var result = await replay.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1_000, result.GetProperty("decision").GetProperty("incrementalUncoveredGoldMg").GetInt64());
        }
        await fixture.RestartApplicationAsync();
        await fixture.AwaitBuyAsync(Id(incremental), "Completed");
        Assert.Equal(2, await fixture.ReadApplicationAsync("""SELECT count(*) FROM "ProcurementIntents" """));
        Assert.Equal(3_000, await fixture.ReadApplicationAsync(
            """SELECT sum("UncoveredGoldMg")::bigint FROM "ProcurementIntents" """));
        await fixture.SetBackingAsync(false);
    }

    private static Guid Id(JsonElement order) => order.GetProperty("orderId").GetGuid();

    private Task<JsonElement> BuyAsync(long mg, string status) => fixture.BuyAsync(mg * 1_000, status);
}
