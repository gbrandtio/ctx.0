using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ctx0.Security.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ctx0.Security.EfCore;

/// <summary>
/// Transparent envelope encryption for PII
/// (ENVELOPE_ENCRYPTION_ARCHITECTURE.md): per-row DEKs wrapped by the
/// versioned KEK, AES-256-GCM with per-operation nonces.
///
/// Registered as a SINGLETON (avoids ManyServiceProvidersCreatedWarning);
/// plaintext-restore state is held in a ConditionalWeakTable keyed by the
/// per-request DbContext so concurrent requests can never observe each
/// other's values and nothing outlives the context.
/// </summary>
public sealed class EnvelopeEncryptionInterceptor(AesEncryptionProvider crypto)
    : SaveChangesInterceptor, IMaterializationInterceptor
{
    private const string DekPropertyName = "EncryptedDek";

    /// <summary>
    /// PII discovery: an entity participates by annotating its string
    /// properties with [CtxEncrypted] and exposing an EncryptedDek
    /// property (EXTENDING_THE_TEMPLATE.md §1) — the interceptor has no
    /// knowledge of concrete entity types. Discovered accessors are
    /// cached per CLR type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Accessor?> Accessors = new();

    private sealed record Accessor(PropertyInfo Dek, PropertyInfo[] Pii);

    private static Accessor? AccessorFor(Type type) =>
        Accessors.GetOrAdd(type, static t =>
        {
            var pii = t.GetProperties()
                .Where(p => p.IsDefined(typeof(CtxEncryptedAttribute), inherit: true))
                .ToArray();
            if (pii.Length == 0)
            {
                return null;
            }
            var dek = t.GetProperty(DekPropertyName)
                ?? throw new InvalidOperationException(
                    $"{t.Name} has [CtxEncrypted] properties but no " +
                    $"{DekPropertyName} property to hold the wrapped DEK.");
            return new Accessor(dek, pii);
        });

    private readonly ConditionalWeakTable<DbContext, Dictionary<object, Dictionary<PropertyInfo, string?>>>
        _plaintextCache = [];

    // ---- Writes: encrypt before save, restore plaintext after ----

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        EncryptTrackedEntities(eventData.Context!);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EncryptTrackedEntities(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        RestorePlaintext(eventData.Context!);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        RestorePlaintext(eventData.Context!);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
        => RestorePlaintext(eventData.Context!);

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        RestorePlaintext(eventData.Context!);
        return Task.CompletedTask;
    }

    // ---- Reads: decrypt during materialization ----

    public object InitializedInstance(MaterializationInterceptionData data, object entity)
    {
        if (AccessorFor(entity.GetType()) is not { } accessor)
        {
            return entity;
        }

        var wrappedDek = (string?)accessor.Dek.GetValue(entity);
        if (string.IsNullOrEmpty(wrappedDek))
        {
            return entity; // row predates encryption or holds no PII yet
        }

        var dek = crypto.UnwrapDek(wrappedDek);
        foreach (var property in accessor.Pii)
        {
            if (property.GetValue(entity) is string ciphertext && ciphertext.Length > 0)
            {
                property.SetValue(entity, crypto.DecryptString(dek, ciphertext));
            }
        }
        return entity;
    }

    // ---- Internals ----

    private void EncryptTrackedEntities(DbContext context)
    {
        var cache = _plaintextCache.GetOrCreateValue(context);
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified) ||
                AccessorFor(entry.Entity.GetType()) is not { } accessor)
            {
                continue;
            }

            // Ensure a DEK exists and is wrapped with the CURRENT KEK —
            // any save silently upgrades stale key versions
            // (ENVELOPE_ENCRYPTION_ARCHITECTURE.md §4).
            var wrappedDek = (string?)accessor.Dek.GetValue(entry.Entity);
            byte[] dek;
            if (string.IsNullOrEmpty(wrappedDek))
            {
                dek = crypto.GenerateDek();
                accessor.Dek.SetValue(entry.Entity, crypto.WrapDek(dek));
            }
            else
            {
                dek = crypto.UnwrapDek(wrappedDek);
                if (!crypto.IsCurrentVersion(wrappedDek))
                {
                    accessor.Dek.SetValue(entry.Entity, crypto.WrapDek(dek));
                }
            }

            var plaintexts = new Dictionary<PropertyInfo, string?>();
            foreach (var property in accessor.Pii)
            {
                var plaintext = (string?)property.GetValue(entry.Entity);
                plaintexts[property] = plaintext;
                if (!string.IsNullOrEmpty(plaintext))
                {
                    property.SetValue(entry.Entity, crypto.EncryptString(dek, plaintext));
                }
            }
            cache[entry.Entity] = plaintexts;
        }
    }

    private void RestorePlaintext(DbContext context)
    {
        if (!_plaintextCache.TryGetValue(context, out var cache))
        {
            return;
        }
        foreach (var (entity, plaintexts) in cache)
        {
            foreach (var (property, plaintext) in plaintexts)
            {
                property.SetValue(entity, plaintext);
            }
        }
        cache.Clear();
    }
}
