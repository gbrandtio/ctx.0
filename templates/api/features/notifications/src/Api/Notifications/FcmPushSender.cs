using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CtxApp.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CtxApp.Api.Notifications;

/// <summary>Options for FCM delivery, read from the <c>NOTIFICATIONS_FCM_*</c> environment variables.</summary>
public sealed class FcmOptions
{
    /// <summary>Firebase project id. When empty, push falls back to the logging sender.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Service-account key: the JSON content inline, or a path to the JSON file.</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>Read the options from the <c>NOTIFICATIONS_FCM_*</c> environment variables, falling back to defaults.</summary>
    public static FcmOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new FcmOptions();
        return new FcmOptions
        {
            ProjectId = configuration["NOTIFICATIONS_FCM_PROJECT_ID"] ?? defaults.ProjectId,
            ServiceAccountJson = configuration["NOTIFICATIONS_FCM_SERVICE_ACCOUNT_JSON"] ?? defaults.ServiceAccountJson,
        };
    }
}

/// <summary>
/// Delivers push notifications through the Firebase Cloud Messaging HTTP v1 API
/// (which relays to APNs for iOS). It authenticates with a Google service account
/// with no third-party SDK: it mints an RS256 JWT from the account's private key,
/// exchanges it for a short-lived OAuth2 access token (cached until it nears
/// expiry), and posts one message per device token. Activated by
/// <see cref="NotificationsBootstrap.AddCtxNotifications"/> when
/// <c>NOTIFICATIONS_FCM_PROJECT_ID</c> is configured.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    private static readonly HttpClient Http = new();

    private readonly FcmOptions _options;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly ServiceAccount _account;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    public FcmPushSender(FcmOptions options, ILogger<FcmPushSender> logger)
    {
        _options = options;
        _logger = logger;
        _account = ServiceAccount.Load(options.ServiceAccountJson);
    }

    public async Task SendAsync(IReadOnlyList<string> tokens, string title, string body, CancellationToken cancellationToken = default)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";

        foreach (var token in tokens)
        {
            var payload = JsonSerializer.Serialize(new
            {
                message = new
                {
                    token,
                    notification = new { title, body },
                },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("FCM delivery failed ({Status}): {Detail}", (int)response.StatusCode, detail);
            }
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            var now = DateTimeOffset.UtcNow;
            var assertion = SignJwt(
                new
                {
                    iss = _account.ClientEmail,
                    scope = "https://www.googleapis.com/auth/firebase.messaging",
                    aud = _account.TokenUri,
                    iat = now.ToUnixTimeSeconds(),
                    exp = now.AddHours(1).ToUnixTimeSeconds(),
                },
                _account.PrivateKey);

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            });
            using var response = await Http.PostAsync(_account.TokenUri, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var token = json.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = json.RootElement.GetProperty("expires_in").GetInt32();
            _cachedToken = token;
            _tokenExpiry = now.AddSeconds(expiresIn - 60);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string SignJwt(object claims, RSA key)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        var signingInput = $"{header}.{payload}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Parsed Google service-account credentials.</summary>
    private sealed record ServiceAccount(string ClientEmail, string TokenUri, RSA PrivateKey)
    {
        public static ServiceAccount Load(string jsonOrPath)
        {
            var json = File.Exists(jsonOrPath) ? File.ReadAllText(jsonOrPath) : jsonOrPath;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var email = root.GetProperty("client_email").GetString()!;
            var tokenUri = root.TryGetProperty("token_uri", out var t)
                ? t.GetString()!
                : "https://oauth2.googleapis.com/token";
            var rsa = RSA.Create();
            rsa.ImportFromPem(root.GetProperty("private_key").GetString());
            return new ServiceAccount(email, tokenUri, rsa);
        }
    }
}
