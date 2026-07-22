namespace CtxApp.Api.Configuration;

/// <summary>
/// Typed view over the process environment. Every setting the host reads from
/// configuration is surfaced here as a descriptively named property, so the rest
/// of the API depends on this class rather than on raw <see cref="IConfiguration"/>
/// keys. Built once at startup by <see cref="FromConfiguration"/> and registered as
/// a singleton in <see cref="ServiceRegistration.AddCtxServices"/>.
/// </summary>
/// <remarks>
/// The security plane reads its own <c>CTX_*</c> variables inside
/// <c>AddCtxSecurity</c>, which already takes <see cref="IConfiguration"/>; those
/// stay encapsulated there and are deliberately not duplicated here.
/// </remarks>
public sealed class EnvironmentSettings
{
    /// <summary>PostgreSQL connection string (<c>CONNECTION_STRINGS_DEFAULT</c>).</summary>
    public required string ConnectionStringsDefault { get; init; }

    /// <summary>
    /// Read and validate the settings from configuration. Throws when a required
    /// variable is missing so the host fails fast at startup rather than at first use.
    /// </summary>
    public static EnvironmentSettings FromConfiguration(IConfiguration configuration)
    {
        return new EnvironmentSettings
        {
            ConnectionStringsDefault = Require(configuration, "CONNECTION_STRINGS_DEFAULT"),
        };
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required environment variable '{key}' is not set.");
        }

        return value;
    }
}
