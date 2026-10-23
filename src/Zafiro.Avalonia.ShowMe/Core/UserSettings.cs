using System.Text.Json;
using CSharpFunctionalExtensions;

namespace Zafiro.Avalonia.ShowMe.Core;

/// <summary>
/// Preferences kept between runs, whatever way ShowMe is started (terminal or file manager).
/// </summary>
public sealed record UserSettings(string? Editor = null);

public interface IUserSettingsStore
{
    UserSettings Load();
    Result Save(UserSettings settings);
}

/// <summary>
/// Keeps the settings in a JSON file, by default in the user's configuration folder.
/// </summary>
public sealed class JsonUserSettingsStore(string path) : IUserSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Zafiro.Avalonia.ShowMe",
        "settings.json");

    public UserSettings Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), Options) ?? new UserSettings()
                : new UserSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new UserSettings();
        }
    }

    public Result Save(UserSettings settings)
    {
        return Result.Try(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
        }, e => $"No se pudo guardar la configuración en {path}: {e.Message}");
    }
}
