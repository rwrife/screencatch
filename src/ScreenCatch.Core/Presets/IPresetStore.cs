namespace ScreenCatch.Core.Presets;

public interface IPresetStore
{
    Task SaveAsync(RecordingPreset preset, CancellationToken cancellationToken = default);

    Task<RecordingPreset?> LoadAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
