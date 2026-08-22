namespace ScreenCatch.Core.Recording;

public enum RecordingSessionState
{
    Idle,
    Running,
    Paused,
    Stopping,
    Encoding,
    Completed,
    Canceled,
    Failed,
}
