using CtxApp.Application.Media;
using CtxApp.Infrastructure.Media;
using CtxApp.Infrastructure.Security.Rls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CtxApp.Api.Media;

/// <summary>
/// Registration surface for the media feature. The base <c>Program.cs</c> calls
/// <see cref="AddCtxMedia"/> during service configuration: it declares per-user
/// RLS isolation for the media table, binds <see cref="MediaOptions"/>, and wires
/// the filesystem blob store (encrypted at rest via the security plane).
/// </summary>
public static class MediaBootstrap
{
    public static IServiceCollection AddCtxMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new RlsPolicy("media", "UserId"));

        var options = new MediaOptions();
        configuration.GetSection(MediaOptions.Section).Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<IBlobStore, LocalBlobStore>();

        return services;
    }
}
