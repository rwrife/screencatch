using System.Runtime.InteropServices;

namespace ScreenCatch.Core.Cursor;

public sealed class SystemCursorStateProvider : ICursorStateProvider
{
    public CursorState? GetCurrentState()
    {
        if (OperatingSystem.IsWindows() && GetCursorPos(out var point))
        {
            return new CursorState(point.X, point.Y, (GetAsyncKeyState(1) & 0x8000) != 0);
        }

        if (OperatingSystem.IsMacOS())
        {
            var nativeEvent = CGEventCreate(IntPtr.Zero);
            if (nativeEvent == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var location = CGEventGetLocation(nativeEvent);
                var pressed = CGEventSourceButtonState(0, 0);
                return new CursorState(
                    checked((int)Math.Round(location.X)),
                    checked((int)Math.Round(location.Y)),
                    pressed);
            }
            finally
            {
                CFRelease(nativeEvent);
            }
        }

        return null;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern NativePointD CGEventGetLocation(IntPtr nativeEvent);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGEventSourceButtonState(int stateId, int button);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePointD
    {
        public readonly double X;
        public readonly double Y;
    }
}
