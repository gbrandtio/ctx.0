using Application.Abstractions;
using Domain.Entities;
using MediatR;

namespace Application.Features.Exports;

public sealed record RequestUserExportCommand(long UserId) : IRequest;

/// <summary>
/// GDPR data export — STUB. This records a Pending export request row so
/// the client's "export my data" action is acknowledged, but the template
/// ships NO fulfillment: there is no worker that assembles the archive, no
/// download endpoint, and no completion notification. Implementing the
/// actual export (collect the user's rows, produce a downloadable artifact,
/// notify on completion) is left to the application developer — model a
/// worker on PostgresNotificationListener's outbox pattern. Until then, do
/// not tell users their data will be delivered.
/// </summary>
public sealed class RequestUserExportHandler(
    IUserExportRepository exports,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<RequestUserExportCommand>
{
    public async Task Handle(RequestUserExportCommand command, CancellationToken ct)
    {
        exports.Add(new UserExport
        {
            Id = ids.NextId(),
            UserId = command.UserId,
            Status = UserExport.Statuses.Pending,
            RequestedAt = clock.UtcNow,
        });
        await exports.SaveChangesAsync(ct);
    }
}
