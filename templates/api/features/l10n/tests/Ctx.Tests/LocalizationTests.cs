using System.Globalization;
using CtxApp.Api.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Xunit;

namespace CtxApp.Tests;

/// <summary>
/// Covers the language contract of the API: the culture is taken from the
/// request's <c>Accept-Language</c> header, anything unsupported falls back to
/// the neutral culture, and the message catalogue answers in the resolved one.
///
/// The assertions are written against whatever languages this workspace was
/// generated with (<see cref="SupportedCultures.Codes"/>) rather than a fixed
/// set, so they hold for any locale selection made at create time. Only the
/// localization pipeline is exercised — no database and no feature endpoints —
/// so these tests stay valid whichever features are enabled.
/// </summary>
public class LocalizationTests
{
    /// <summary>A second language, when the workspace ships one.</summary>
    private static string? SecondCulture => SupportedCultures.Codes.Skip(1).FirstOrDefault();

    [Fact]
    public void The_first_supported_culture_is_the_fallback()
    {
        Assert.Equal(LocalizationBootstrap.DefaultCulture, SupportedCultures.Codes[0]);
    }

    [Fact]
    public async Task A_request_is_served_in_the_language_it_asks_for()
    {
        var second = SecondCulture;
        if (second is null)
        {
            return; // English-only workspace: nothing to negotiate.
        }

        using var host = await StartAsync();
        var response = await GetCultureAsync(host, second);

        Assert.Equal(second, response);
    }

    [Fact]
    public async Task An_unsupported_language_falls_back_to_the_default()
    {
        using var host = await StartAsync();

        Assert.Equal(LocalizationBootstrap.DefaultCulture, await GetCultureAsync(host, "zz"));
        Assert.Equal(LocalizationBootstrap.DefaultCulture, await GetCultureAsync(host, null));
    }

    [Fact]
    public void Every_message_is_translated_into_every_supported_language()
    {
        using var provider = new ServiceCollection().AddLogging().AddCtxLocalization().BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<Messages>>();

        // The key list comes from the neutral resource set: it is the one embedded
        // in the assembly itself, and `GetAllStrings` can only read that one — the
        // per-culture translations live in satellite assemblies, which is what the
        // indexed lookups below exercise.
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        var keys = localizer.GetAllStrings(includeParentCultures: false).Select(s => s.Name).ToList();
        Assert.NotEmpty(SupportedCultures.Codes);

        foreach (var culture in SupportedCultures.Codes)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            foreach (var key in keys)
            {
                var message = localizer[key];
                Assert.False(message.ResourceNotFound, $"{key} is missing for '{culture}'.");
                Assert.NotEmpty(message.Value);
            }
        }
    }

    [Fact]
    public void An_unknown_key_degrades_to_the_key_itself()
    {
        using var provider = new ServiceCollection().AddLogging().AddCtxLocalization().BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<Messages>>();

        var message = localizer["nope.notAKey"];

        Assert.True(message.ResourceNotFound);
        Assert.Equal("nope.notAKey", message.Value);
    }

    /// <summary>A minimal pipeline: localization middleware, then echo the culture.</summary>
    private static async Task<IHost> StartAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddCtxLocalization())
                .Configure(app =>
                {
                    app.UseCtxLocalization();
                    app.Run(context => context.Response.WriteAsync(CultureInfo.CurrentUICulture.Name));
                }))
            .StartAsync();
        return host;
    }

    private static async Task<string> GetCultureAsync(IHost host, string? acceptLanguage)
    {
        var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (acceptLanguage is not null)
        {
            request.Headers.Add("Accept-Language", acceptLanguage);
        }

        var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}
