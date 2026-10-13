using System.Diagnostics;
using System.Text.Json;
using Handy.Abstractions;

namespace Handy.Services;

/// <summary>Key/value settings kept in one JSON file; every <see cref="Set{T}"/> saves it.</summary>
internal sealed class JsonSettingsStore : IModuleSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Dictionary<string, JsonElement> _values;

    public JsonSettingsStore(string path)
    {
        _path = path;
        _values = Load(path);
    }

    public T? Get<T>(string key, T? fallback = default)
    {
        if (!_values.TryGetValue(key, out var value))
            return fallback;
        try
        {
            return value.Deserialize<T>() ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    public void Set<T>(string key, T value)
    {
        _values[key] = JsonSerializer.SerializeToElement(value);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_values, SerializerOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing a setting is not worth crashing the app.
            Trace.TraceWarning($"Could not save settings to '{_path}': {e.Message}");
        }
    }

    private static Dictionary<string, JsonElement> Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path)) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceWarning($"Could not read settings from '{path}': {e.Message}");
        }
        return [];
    }
}
