using System.Text.Json;

namespace Acme.Tests.Security;

/// <summary>
/// Loads the shared wire-protocol golden vectors that the Flutter client and
/// this API both assert against. The file is synced into the workspace at
/// <c>.ctx/vectors.json</c> when the workspace is generated.
/// </summary>
public static class GoldenVectors
{
    public static JsonElement Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".ctx", "vectors.json");
            if (File.Exists(candidate))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                return doc.RootElement.Clone();
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate .ctx/vectors.json above " + AppContext.BaseDirectory);
    }

    public static byte[] B64(this JsonElement e, string prop) => Convert.FromBase64String(e.GetProperty(prop).GetString()!);

    public static string Str(this JsonElement e, string prop) => e.GetProperty(prop).GetString()!;
}
