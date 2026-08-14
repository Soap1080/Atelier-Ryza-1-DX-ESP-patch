using System.Text.Json;

namespace RyzaEsPatcher.Core;

/// <summary>Preferencias de la interfaz. Se guardan para que la elección del usuario no se pierda.</summary>
public sealed record UiSettings(bool DarkMode)
{
    /// <summary>El modo oscuro es el predeterminado.</summary>
    public static UiSettings Default => new(DarkMode: true);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RyzaDX-ParcheES",
        "ui.json");

    /// <summary>Nunca falla: si no hay archivo o está corrupto, devuelve los valores por defecto.</summary>
    public static UiSettings Load(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            if (!File.Exists(file)) return Default;

            return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(file), Options) ?? Default;
        }
        catch (JsonException) { return Default; }
        catch (IOException) { return Default; }
        catch (UnauthorizedAccessException) { return Default; }
    }

    /// <summary>Nunca falla: si no se puede guardar, simplemente no se recuerda la preferencia.</summary>
    public void Save(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Options));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
