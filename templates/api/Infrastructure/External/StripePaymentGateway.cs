using Application.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.External;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Injected via STRIPE_SECRET_KEY; never in appsettings.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Webhook signing secret (whsec_...), via STRIPE_WEBHOOK_SECRET.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// Stripe PaymentIntents (PAYMENTS_STRIPE.md §3): the amount comes from
/// the server-side order row; metadata carries the correlation ids the
/// webhook re-validates; the idempotency key payment-intent:{orderId}:{userId}
/// makes a single consumer's retries safe without colliding when two
/// different consumers attempt the same order (M5).
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly PaymentIntentService _paymentIntents;

    public StripePaymentGateway(IOptions<StripeOptions> options)
    {
        var client = new StripeClient(options.Value.SecretKey);
        _paymentIntents = new PaymentIntentService(client);
    }

    public async Task<(string PaymentIntentId, string ClientSecret)> CreatePaymentIntentAsync(
        long orderId, long amountMinor, string currency,
        long userId, long projectId, CancellationToken ct)
    {
        var intent = await _paymentIntents.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = amountMinor,
                Currency = currency.ToLowerInvariant(),
                AutomaticPaymentMethods =
                    new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = orderId.ToString(),
                    ["userId"] = userId.ToString(),
                    ["projectId"] = projectId.ToString(),
                },
            },
            new RequestOptions { IdempotencyKey = $"payment-intent:{orderId}:{userId}" },
            ct);
        return (intent.Id, intent.ClientSecret);
    }
}
