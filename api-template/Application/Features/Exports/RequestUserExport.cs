using Application.Abstractions;
using Domain.Entities;
using MediatR;
using SharedKernel.Clock;

namespace Application.Features.Exports;

public sealed record RequestUserExportCommand(long UserId) : IRequest;

/// <summary>
/// GDPR data export: records the request and writes the completion
/// notification path through the outbox when the background worker
/// finishes (APP_SHELL.md §4 client contract).
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
