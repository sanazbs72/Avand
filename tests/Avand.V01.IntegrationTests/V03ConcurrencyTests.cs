using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

/// <summary>T020 / AC-10, AC-11: two sells against one customer's gold, two customers against shared liquidity,
/// and a sale racing a V01 capture credit. Gold never goes negative and accepted nets never exceed liquidity.</summary>
public sealed class V03ConcurrencyTests(V03Fixture fixture) : IClassFixture<V03Fixture>
{
    [Fact]
    public async Task TwoSalesAgainstOneCustomersGoldPostOnce()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        await fixture.SetFreeLiquidityAsync(50_000_000);
        var gold = await fixture.GoldMgAsync(fixture.CustomerId);
        var wallet = await fixture.AvailableAsync(fixture.CustomerId);
        fixture.ObserveSellPrice();
        var first = await fixture.SellQuoteAsync(quantityMg: gold);
        var second = await fixture.SellQuoteAsync(quantityMg: gold);

        var orders = await Task.WhenAll(
            fixture.ConfirmedSellAsync(first.GetProperty("quoteId").GetGuid()),
            fixture.ConfirmedSellAsync(second.GetProperty("quoteId").GetGuid()));
        var results = await Task.WhenAll(orders.Select(id => TerminalAsync(id)));

        Assert.Single(results, x => x.Status == "Completed");
        Assert.Single(results, x => x is { Status: "Rejected", Reason: "InsufficientGold" });
        Assert.Equal(0, await fixture.GoldMgAsync(fixture.CustomerId));
        Assert.Equal(wallet + results.Single(x => x.Status == "Completed").Net, await fixture.AvailableAsync(fixture.CustomerId));
    }

    [Fact]
    public async Task TwoCustomersCompetingForSharedLiquidityPostOnlyWhatFits()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        await fixture.GiveGoldAsync(fixture.OtherId, 500, fixture.Other);
        const long initial = 4_875_500 + 4_875_499; // one net fits, two do not
        await fixture.SetFreeLiquidityAsync(initial);
        fixture.ObserveSellPrice();
        var mine = await fixture.SellQuoteAsync(quantityMg: 500);
        var theirs = await fixture.SellQuoteAsync(quantityMg: 500, customer: fixture.Other);

        var orders = await Task.WhenAll(
            fixture.ConfirmedSellAsync(mine.GetProperty("quoteId").GetGuid()),
            fixture.ConfirmedSellAsync(theirs.GetProperty("quoteId").GetGuid(), customer: fixture.Other));
        var results = new[] { await TerminalAsync(orders[0]), await TerminalAsync(orders[1], fixture.Other) };

        Assert.Single(results, x => x.Status == "Completed");
        Assert.Single(results, x => x is { Status: "Rejected", Reason: "SalesTemporarilyUnavailable" });
        Assert.Equal(initial - 4_875_500, await fixture.FreeLiquidityAsync());
        Assert.True(results.Where(x => x.Status == "Completed").Sum(x => x.Net) <= initial);
    }

    [Fact]
    public async Task ASaleRacingACaptureCreditIsDecidedAgainstASerializedLiquidity()
    {
        await fixture.GiveGoldAsync(fixture.CustomerId, 500);
        const long net = 4_875_500;
        const long credit = 1_000_000;
        await fixture.SetFreeLiquidityAsync(net);
        fixture.ObserveSellPrice();
        var quote = await fixture.SellQuoteAsync(quantityMg: 500);
        var topUp = await fixture.StartAsync(fixture.Other, amountIrr: credit);
        await fixture.AwaitStatusAsync(topUp, "AwaitingProvider", fixture.Other);
        fixture.Gateway.SetResult(topUp, "Ready");

        var sale = fixture.ConfirmedSellAsync(quote.GetProperty("quoteId").GetGuid());
        await Task.WhenAll(sale, fixture.NotifyAsync(topUp));
        await fixture.AwaitStatusAsync(topUp, "Succeeded", fixture.Other);
        var result = await TerminalAsync(await sale);

        // The capture always credits once. Whichever took the guard first, the sale saw consistent liquidity.
        Assert.Equal(1, await fixture.PostingCountAsync(topUp));
        if (result.Status == "Completed")
            Assert.Equal(-credit, await fixture.FreeLiquidityAsync());
        else
        {
            Assert.Equal("SalesTemporarilyUnavailable", result.Reason);
            Assert.Equal(net - credit, await fixture.FreeLiquidityAsync());
        }
    }

    private async Task<(string Status, string? Reason, long Net)> TerminalAsync(Guid orderId, HttpClient? customer = null)
    {
        JsonElement order = default;
        await V01Fixture.EventuallyAsync(async () =>
        {
            order = await (customer ?? fixture.Customer).GetFromJsonAsync<JsonElement>($"/api/v1/gold/sell-orders/{orderId}");
            return order.GetProperty("status").GetString() is "Completed" or "Rejected";
        });
        return (order.GetProperty("status").GetString()!,
            order.GetProperty("reasonCode").ValueKind == JsonValueKind.Null ? null : order.GetProperty("reasonCode").GetString(),
            order.GetProperty("netIrr").GetInt64());
    }
}
