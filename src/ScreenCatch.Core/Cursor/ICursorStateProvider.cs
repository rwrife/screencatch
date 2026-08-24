namespace ScreenCatch.Core.Cursor;

public interface ICursorStateProvider
{
    CursorState? GetCurrentState();
}
