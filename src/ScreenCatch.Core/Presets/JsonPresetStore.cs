using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenCatch.Core.Presets;

public sealed class JsonPresetStore : IPresetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _directory;

    public JsonPresetStore(string? directory = null)
    {
        _directory = directory ?? GetDefaultDirectory();
    }

    public async Task SaveAsync(RecordingPreset preset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ValidateName(preset.Name);
        preset.Validate();

        Directory.CreateDirectory(_directory);
        var path = GetPath(preset.Name);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, preset, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async Task<RecordingPreset?> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        var path = GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var preset = await JsonSerializer.DeserializeAsync<RecordingPreset>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return preset?.Validate();
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_directory))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var names = Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(name);
        var path = GetPath(name);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    public static string GetDefaultDirectory()
    {
        var root = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(root, "screencatch", "presets");
    }

    private string GetPath(string name) => Path.Combine(_directory, $"{name}.json");

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name is "." or ".."
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Preset names must be non-empty file names without path separators.", nameof(name));
        }
    }
}
