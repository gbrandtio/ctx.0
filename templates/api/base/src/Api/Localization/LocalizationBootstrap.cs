using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace CtxApp.Api.Localization;

/// <summary>
/// Registration surface for localization. It is part of the always-on base, so
/// the API answers in the caller's language in every workspace: the base
/// <c>Program.cs</c> calls <see cref="AddCtxLocalization"/> during service
/// configuration and <see cref="UseCtxLocalization"/> when building the pipeline.
/// </summary>
/// <remarks>
/// The supported cultures come from <see cref="SupportedCultures.Codes"/>, which
/// ctx.0 generates from the languages chosen at create time — the same set the app
/// ships — so adding a language means regenerating the workspace rather than
/// editing this file. The neutral
/// <c>Messages.resx</c> is the fallback for any culture that is not supported,
/// which is why a request with no (or an unknown) <c>Accept-Language</c> still
/// gets a sensible answer.
/// </remarks>
public static class LocalizationBootstrap
{
    /// <summary>The culture every unsupported request falls back to.</summary>
    public const string DefaultCulture = "en";

    public static IServiceCollection AddCtxLocalization(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        var cultures = SupportedCultures.Codes.Select(code => new CultureInfo(code)).ToArray();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(DefaultCulture);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

        return services;
    }

    /// <summary>
    /// Resolve the request's culture from its <c>Accept-Language</c> header. Must
    /// run before anything that produces user-facing text.
    /// </summary>
    public static IApplicationBuilder UseCtxLocalization(this IApplicationBuilder app)
    {
        return app.UseRequestLocalization();
    }
}
