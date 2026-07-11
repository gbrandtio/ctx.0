using System.Collections;
using System.Reflection;
using Ganss.Xss;

namespace AppApi.Filters;

/// <summary>
/// Global XSS input sanitization (FILTERS_AND_MIDDLEWARE.md §1,
/// APPLICATION_LAYER_SECURITY.md §5): recursively strips dangerous
/// HTML/JS from every string property of incoming DTOs (depth-limited to
/// 10; supports records with init-only setters, lists, arrays, and
/// string dictionaries).
/// </summary>
public sealed class SanitizationFilter : IEndpointFilter
{
    private const int MaxDepth = 10;
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is not null)
            {
                SanitizeObject(argument, depth: 0);
            }
        }
        return await next(context);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        // Plain text only: strip every tag/attribute, keep the safe text.
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.KeepChildNodes = true;
        return sanitizer;
    }

    private static void SanitizeObject(object target, int depth)
    {
        if (depth >= MaxDepth)
        {
            return;
        }

        var type = target.GetType();
        if (type.IsPrimitive || type.IsValueType || target is string)
        {
            return; // scalars are handled by their owners
        }

        switch (target)
        {
            case IList list:
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is string s)
                    {
                        list[i] = Sanitize(s);
                    }
                    else if (list[i] is not null)
                    {
                        SanitizeObject(list[i]!, depth + 1);
                    }
                }
                return;
            case IDictionary<string, string> dictionary:
                foreach (var key in dictionary.Keys.ToList())
                {
                    dictionary[key] = Sanitize(dictionary[key]);
                }
                return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }
            var value = property.GetValue(target);
            switch (value)
            {
                case string s when property.CanWrite:
                    // Reflection writes work for record init-only setters too.
                    property.SetValue(target, Sanitize(s));
                    break;
                case null or string:
                    break;
                default:
                    if (!property.PropertyType.IsValueType)
                    {
                        SanitizeObject(value, depth + 1);
                    }
                    break;
            }
        }
    }

    private static string Sanitize(string input) => Sanitizer.Sanitize(input);
}
