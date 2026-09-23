using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Events;
using Kinsat.Sdk.Events;

namespace Kinsat.Core.Tests;


public class EventBusTests
{
    [Fact]
    public async Task Subscriber_receives_a_published_event()
    {
        var bus = new InProcessEventBus();
        var received = new List<LtvBreached>();
        bus.Subscribe<LtvBreached>((e, _) => { received.Add(e); return Task.CompletedTask; });

        await bus.Publish(new LtvBreached("mfi-lagos", DateTimeOffset.UtcNow, "loan-001", 0.82m, "margin-call-tier-2"));

        Assert.Single(received);
        Assert.Equal("loan-001", received[0].LoanId);
    }

    [Fact]
    public async Task Multiple_subscribers_to_the_same_event_all_receive_it()
    {
        var bus = new InProcessEventBus();
        var callsA = 0;
        var callsB = 0;
        bus.Subscribe<LoanOriginated>((_, _) => { callsA++; return Task.CompletedTask; });
        bus.Subscribe<LoanOriginated>((_, _) => { callsB++; return Task.CompletedTask; });

        await bus.Publish(new LoanOriginated("mfi-lagos", DateTimeOffset.UtcNow, "loan-001", "borrower-001"));

        Assert.Equal(1, callsA);
        Assert.Equal(1, callsB);
    }

    [Fact]
    public async Task Subscribers_to_a_different_event_type_are_not_invoked()
    {
        var bus = new InProcessEventBus();
        var repaymentCalls = 0;
        bus.Subscribe<RepaymentReceived>((_, _) => { repaymentCalls++; return Task.CompletedTask; });

        await bus.Publish(new LiquidationExecuted("mfi-lagos", DateTimeOffset.UtcNow, "loan-001", 50000m));

        Assert.Equal(0, repaymentCalls);
    }

    [Fact]
    public async Task Publishing_an_event_with_no_subscribers_does_not_throw()
    {
        var bus = new InProcessEventBus();

        var exception = await Record.ExceptionAsync(() =>
            bus.Publish(new CreditScoreUpdated("mfi-lagos", DateTimeOffset.UtcNow, "borrower-001", 72.5)));

        Assert.Null(exception);
    }
}

