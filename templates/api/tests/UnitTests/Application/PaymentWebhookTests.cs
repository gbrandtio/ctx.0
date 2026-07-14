using Application.Abstractions;
using Application.Features.Payments;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace UnitTests.Application;

public sealed class PaymentWebhookTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ILedgerRepository> _ledger = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IIdGenerator> _ids = new();

    private ProcessPaidPaymentIntentHandler CreateHandler() =>
        new(_orders.Object, _ledger.Object, _notifications.Object, _ids.Object, new TestClock());

    private static ProcessPaidPaymentIntentCommand Command() =>
        new("pi_1", OrderId: 10, UserId: 1, AmountMinor: 500, Currency: "eur");

    [Fact]
    public async Task Duplicate_webhook_event_is_ignored_via_the_ledger_guard()
    {
        _ledger.Setup(l => l.PaymentIntentExistsAsync("pi_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        _orders.Verify(o => o.TryMarkPaidAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _ledger.Verify(l => l.Add(It.IsAny<LedgerEntry>()), Times.Never);
    }

    [Fact]
    public async Task Amount_mismatch_against_the_order_is_rejected()
    {
        _ledger.Setup(l => l.PaymentIntentExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orders.Setup(o => o.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = 10, AmountMinor = 999, Currency = "EUR" });

        // The server-side order is the source of truth: a webhook claiming
        // a different amount must never fulfill.
        await Assert.ThrowsAsync<DomainException>(() =>
            CreateHandler().Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Successful_payment_writes_ledger_and_notification_atomically()
    {
        _ledger.Setup(l => l.PaymentIntentExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orders.Setup(o => o.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = 10, AmountMinor = 500, Currency = "EUR" });
        _orders.Setup(o => o.TryMarkPaidAsync(10, "pi_1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var marked = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(marked); // signals the endpoint to broadcast exactly once
        _ledger.Verify(l => l.Add(It.Is<LedgerEntry>(e => e.StripePaymentIntentId == "pi_1")), Times.Once);
        _notifications.Verify(n => n.Add(It.Is<UserNotification>(x => x.Type == "payment_completed")), Times.Once);
    }

    [Fact]
    public async Task Losing_the_atomic_race_writes_nothing()
    {
        _ledger.Setup(l => l.PaymentIntentExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orders.Setup(o => o.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = 10, AmountMinor = 500, Currency = "EUR" });
        _orders.Setup(o => o.TryMarkPaidAsync(10, "pi_1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // another concurrent event already consumed it

        var marked = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.False(marked); // no broadcast on the losing race
        _ledger.Verify(l => l.Add(It.IsAny<LedgerEntry>()), Times.Never);
        _notifications.Verify(n => n.Add(It.IsAny<UserNotification>()), Times.Never);
    }
}
