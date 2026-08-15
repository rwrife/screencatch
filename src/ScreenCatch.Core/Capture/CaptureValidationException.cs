namespace ScreenCatch.Core.Capture;

public sealed class CaptureValidationException : Exception
{
    public CaptureValidationException(string message)
        : base(message)
    {
    }
}
