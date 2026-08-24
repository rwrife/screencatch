namespace ScreenCatch.Core.Cursor;

public sealed record CursorOverlayOptions(bool HighlightEnabled, bool ClickEffectsEnabled)
{
    public bool IsEnabled => HighlightEnabled || ClickEffectsEnabled;
}
