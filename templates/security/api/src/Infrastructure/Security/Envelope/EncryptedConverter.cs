using CtxApp.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CtxApp.Infrastructure.Security.Envelope;

/// <summary>
/// EF value converter that transparently envelope-encrypts a string on the way
/// to the database and decrypts it on the way back.
/// </summary>
public sealed class EncryptedConverter(IFieldCipher cipher)
    : ValueConverter<string, string>(v => cipher.Encrypt(v), v => cipher.Decrypt(v));
