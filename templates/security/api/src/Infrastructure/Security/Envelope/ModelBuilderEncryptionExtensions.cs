using System.Reflection;
using CtxApp.Application.Abstractions;
using CtxApp.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Security.Envelope;

/// <summary>Applies envelope encryption to every property marked <see cref="EncryptedAttribute"/>.</summary>
public static class ModelBuilderEncryptionExtensions
{
    public static void ApplyCtxEncryption(this ModelBuilder modelBuilder, IFieldCipher cipher)
    {
        var converter = new EncryptedConverter(cipher);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(string)
                    && property.PropertyInfo?.GetCustomAttribute<EncryptedAttribute>() is not null)
                {
                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
