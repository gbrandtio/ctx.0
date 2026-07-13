using System.Security.Claims;
using AppApi.Endpoints;
using Application.Features.Orders;
using Contracts.Orders;
using Domain.Constants;
using MediatR;

namespace AppApi.Endpoints.v1;

/// <summary>
/// Member-actor order creation (AUTHORIZATION.md §7): the OrderManage
/// policy requires orders:manage AND membership of the route's
/// {projectId}.
/// </summary>
public sealed class OrdersEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder v1)
    {
        v1.MapPost("/projects/{projectId:long}/orders", async (
                long projectId, CreateOrderRequest request,
                ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
            {
                var memberUserId = long.Parse(
                    user.FindFirstValue(SecurityConstants.ClaimTypes.UserId)!);
                var response = await mediator.Send(
                    new CreateOrderCommand(projectId, memberUserId, request), ct);
                return Results.Created($"/v1/projects/{projectId}/orders/{response.Id}", response);
            })
            .RequireAuthorization(SecurityConstants.Policies.OrderManage);
    }
}
