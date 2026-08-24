using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScreenCatch.App.RegionSelection;
using ScreenCatch.App.ViewModels;

namespace ScreenCatch.App;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private RecordingViewModel? ViewModel => DataContext as RecordingViewModel;

    private async void SelectRegion_Click(object? sender, RoutedEventArgs e)
    {
        var screens = Screens.All;
        if (screens.Count == 0 || ViewModel is null)
        {
            return;
        }

        var left = screens.Min(screen => screen.Bounds.X);
        var top = screens.Min(screen => screen.Bounds.Y);
        var right = screens.Max(screen => screen.Bounds.Right);
        var bottom = screens.Max(screen => screen.Bounds.Bottom);
        var bounds = new PixelRect(left, top, right - left, bottom - top);
        var scaling = Screens.ScreenFromWindow(this)?.Scaling ?? screens[0].Scaling;
        var overlay = new RegionSelectionWindow(bounds, scaling);
        var region = await overlay.ShowDialog<ScreenCatch.Core.Capture.CaptureRect?>(this);
        if (region is not null)
        {
            ViewModel.SetRegion(region.Value);
        }
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.OutputPath is not { } sourcePath || !File.Exists(sourcePath))
        {
            return;
        }

        var destination = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save ScreenCatch recording",
            SuggestedFileName = Path.GetFileName(sourcePath),
            DefaultExtension = Path.GetExtension(sourcePath).TrimStart('.'),
        });
        if (destination is null)
        {
            return;
        }

        await using var source = File.OpenRead(sourcePath);
        await using var target = await destination.OpenWriteAsync();
        target.SetLength(0);
        await source.CopyToAsync(target);
    }

    private async void CopyPath_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.OutputPath is not { } outputPath)
        {
            return;
        }

        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(outputPath);
        }
    }

    private void OpenLocation_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.OutputPath is not { } outputPath)
        {
            return;
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var command = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        Process.Start(new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = true,
            ArgumentList = { directory },
        });
    }
}
