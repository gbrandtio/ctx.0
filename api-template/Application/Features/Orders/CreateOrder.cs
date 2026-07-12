using Application.Abstractions;
using Contracts.Orders;
using Domain.Entities;
using Domain.ValueObjects;
using MediatR;
using SharedKernel.Clock;

namespace Application.Features.Orders;

public sealed record CreateOrderCommand(
    long ProjectId, long MemberUserId, CreateOrderRequest Request) : IRequest<OrderResponse>;

/// <summary>
/// Member actor creates the server-issued, single-use order record — the
/// authoritative amount for the later consumer payment
/// (AUTHORIZATION.md §7, PAYMENTS_STRIPE.md).
/// </summary>
public sealed class CreateOrderHandler(
    IOrderRepository orders,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    public async Task<OrderResponse> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var money = Money.Create(command.Request.AmountMinor, command.Request.Currency);

        var order = new Order
        {
            Id = ids.NextId(),
            ProjectId = command.ProjectId,
            CreatedByMemberUserId = command.MemberUserId,
            AmountMinor = money.AmountMinor,
            Currency = money.Currency,
            Status = Order.Statuses.Pending,
            CreatedAt = clock.UtcNow,
        };
        orders.Add(order);
        await orders.SaveChangesAsync(ct);

        return new OrderResponse(
            order.Id, order.ProjectId, order.AmountMinor, order.Currency,
            order.Status, order.CreatedAt);
    }
}
