using CtxApp.Application.Abstractions;
using CtxApp.Application.Media;
using CtxApp.Infrastructure.Gdpr;
using CtxApp.Infrastructure.Media;
using CtxApp.Infrastructure.Security.Rls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CtxApp.Api.Media;

/// <summary>
/// Registration surface for the media feature. The base <c>Program.cs</c> calls
/// <see cref="AddCtxMedia"/> during service configuration: it declares per-user
/// RLS isolation for the media table, reads <see cref="MediaOptions"/>, wires the
/// filesystem blob store (encrypted at rest via the security plane), and declares
/// the personal data the feature holds.
/// </summary>
public static class MediaBootstrap
{
    public static IServiceCollection AddCtxMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new RlsPolicy("media", "UserId"));

        var options = MediaOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        services.AddSingleton<IBlobStore, LocalBlobStore>();

        // Personal data this feature holds — metadata rows plus the blobs
        // themselves — for the gdpr feature's export/erasure.
        services.AddScoped<MediaPersonalData>();
        services.AddScoped<IPersonalDataContributor>(sp => sp.GetRequiredService<MediaPersonalData>());
        services.AddScoped<IPersonalDataAttachments>(sp => sp.GetRequiredService<MediaPersonalData>());

        return services;
    }
}
