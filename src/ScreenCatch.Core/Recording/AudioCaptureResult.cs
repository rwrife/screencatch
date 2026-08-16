namespace ScreenCatch.Core.Recording;

public sealed class AudioCaptureResult
{
    public AudioCaptureResult(string filePath, string? codec = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Audio capture file path is required.", nameof(filePath));
        }

        FilePath = filePath;
        Codec = codec;
    }

    public string FilePath { get; }

    public string? Codec { get; }
}
