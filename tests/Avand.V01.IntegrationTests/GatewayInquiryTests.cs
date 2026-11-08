using Avand.V01.IntegrationTests.Fixtures;
using Xunit;

namespace Avand.V01.IntegrationTests;

public sealed class GatewayInquiryTests(V01Fixture fixture) : IClassFixture<V01Fixture>
{
    [Fact]
    public async Task MissingCallbackAndMismatchedEvidenceStayPendingUntilMatched()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        fixture.Gateway.SetResult(id, "Captured", reportedAmount: 101);
        await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.Gateway.LastOutcome(id) == "Captured"));
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));

        var calls = fixture.Gateway.CallCount(id);
        fixture.Gateway.SetResult(id, "Captured", reportedPayment: Guid.NewGuid());
        await fixture.NotifyAsync(id);
        await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.Gateway.CallCount(id) > calls));
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));

        fixture.Gateway.SetResult(id, "Captured");
        await fixture.AwaitStatusAsync(id, "Succeeded");
        Assert.Equal(before + 100, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
    }

    [Fact]
    public async Task LateCallbackExpiresAndIssuerReturnHasNoFinancialEffect()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        var journals = await fixture.JournalCountAsync();
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        fixture.Gateway.Advance(TimeSpan.FromMinutes(16));
        fixture.Gateway.SetResult(id, "Ready");
        await fixture.NotifyAsync(id);
        var failed = await fixture.AwaitStatusAsync(id, "Failed");
        Assert.Equal("VerifyExpiredIssuerReturnWithin72Hours", failed.GetProperty("messageCode").GetString());
        fixture.Gateway.SetResult(id, "IssuerReturned");
        await fixture.NotifyAsync(id);
        await V01Fixture.EventuallyAsync(async () =>
            (await fixture.AwaitStatusAsync(id, "Failed")).GetProperty("messageCode").GetString() == "IssuerReturnConfirmed");
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(journals, await fixture.JournalCountAsync());
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));
    }

    [Fact]
    public async Task DefinitiveVerifyRejectionFailsWithoutLedgerPosting()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        var journals = await fixture.JournalCountAsync();
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");

        fixture.Gateway.SetResult(id, "Rejected");
        await fixture.NotifyAsync(id);

        var failed = await fixture.AwaitStatusAsync(id, "Failed");
        Assert.Equal("ProviderRejected", failed.GetProperty("messageCode").GetString());
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(journals, await fixture.JournalCountAsync());
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));
    }

    [Fact]
    public async Task GatewayTimeoutRemainsPendingUntilInquiryConfirmsCapture()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        var calls = fixture.Gateway.CallCount(id);

        fixture.Gateway.SetResult(id, "Timeout");
        await fixture.NotifyAsync(id);
        await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.Gateway.CallCount(id) >= calls + 2));
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));

        fixture.Gateway.SetResult(id, "Captured");
        await fixture.NotifyAsync(id);
        await fixture.AwaitStatusAsync(id, "Succeeded");
        Assert.Equal(before + 100, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
    }

    [Fact]
    public async Task CaptureWithoutReferenceRemainsPendingUntilCompleteEvidenceArrives()
    {
        var before = await fixture.AvailableAsync(fixture.CustomerId);
        var id = await fixture.StartAsync();
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        var calls = fixture.Gateway.CallCount(id);

        fixture.Gateway.SetResult(id, "Captured", missingCaptureReference: true);
        await fixture.NotifyAsync(id);
        await V01Fixture.EventuallyAsync(() => Task.FromResult(fixture.Gateway.CallCount(id) >= calls + 2));
        await fixture.AwaitStatusAsync(id, "AwaitingProvider");
        Assert.Equal(before, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(0L, await fixture.DecisionCountAsync(id));

        fixture.Gateway.SetResult(id, "Captured");
        await fixture.NotifyAsync(id);
        await fixture.AwaitStatusAsync(id, "Succeeded");
        Assert.Equal(before + 100, await fixture.AvailableAsync(fixture.CustomerId));
        Assert.Equal(1L, await fixture.PostingCountAsync(id));
    }
}
