using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Clock;

namespace Infrastructure.Repositories.Orders;

public sealed class OrderRepository(AppDbContext db, IClock clock) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(long id, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public void Add(Order order) => db.Orders.Add(order);

    public async Task<bool> TryMarkPaidAsync(
        long orderId, string paymentIntentId, long paidByUserId, CancellationToken ct)
    {
        // Atomic conditional UPDATE: exactly one concurrent consumer wins
        // (single-use order guarantee, PAYMENTS_STRIPE.md §4).
        var updated = await db.Orders
            .Where(o => o.Id == orderId && o.Status == Order.Statuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, Order.Statuses.Paid)
                .SetProperty(o => o.StripePaymentIntentId, paymentIntentId)
                .SetProperty(o => o.PaidByUserId, paidByUserId)
                .SetProperty(o => o.PaidAt, clock.UtcNow), ct);
        return updated == 1;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class LedgerRepository(AppDbContext db) : ILedgerRepository
{
    public Task<bool> PaymentIntentExistsAsync(string paymentIntentId, CancellationToken ct) =>
        db.Ledger.AnyAsync(l => l.StripePaymentIntentId == paymentIntentId, ct);

    public void Add(LedgerEntry entry) => db.Ledger.Add(entry);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
