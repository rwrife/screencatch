using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScreenCatch.App.Services;
using ScreenCatch.App.ViewModels;
using ScreenCatch.Core.Capture;
using ScreenCatch.Core.Cursor;
using ScreenCatch.Core.Recording;

namespace ScreenCatch.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session = new RecordingSession(
                ScreenCaptureSourceFactory.CreateDefault(),
                new FfmpegVideoEncoder(new FfmpegProcessEngine()),
                cursorOverlay: new CursorOverlayRenderer(),
                cursorStateProvider: new SystemCursorStateProvider());
            var viewModel = new RecordingViewModel(
                session,
                new CountdownService(),
                CreateOutputPath);
            var window = new MainWindow
            {
                DataContext = viewModel,
            };
            window.Closed += async (_, _) => await viewModel.DisposeAsync();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string CreateOutputPath()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(videos))
        {
            videos = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(videos, "ScreenCatch", $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
    }
}
