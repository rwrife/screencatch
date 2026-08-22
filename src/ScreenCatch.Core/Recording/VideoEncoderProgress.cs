namespace ScreenCatch.Core.Recording;

public sealed record VideoEncoderProgress(TimeSpan EncodedDuration, TimeSpan ExpectedDuration, double PercentComplete);
