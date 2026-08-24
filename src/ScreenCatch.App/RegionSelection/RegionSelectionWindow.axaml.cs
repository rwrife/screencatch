using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ScreenCatch.App.RegionSelection;

public sealed partial class RegionSelectionWindow : Window
{
    private readonly PixelRect _virtualDesktopBounds;
    private readonly double _renderScaling;
    private readonly Canvas _surface;
    private readonly Border _selection;
    private Point? _dragStart;

    public RegionSelectionWindow()
        : this(new PixelRect(0, 0, 1, 1), 1)
    {
    }

    public RegionSelectionWindow(PixelRect virtualDesktopBounds, double renderScaling)
    {
        if (renderScaling <= 0 || !double.IsFinite(renderScaling))
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

        _virtualDesktopBounds = virtualDesktopBounds;
        _renderScaling = renderScaling;
        InitializeComponent();
        _surface = this.FindControl<Canvas>("SelectionSurface")
            ?? throw new InvalidOperationException("Selection surface is missing.");
        _selection = this.FindControl<Border>("SelectionBorder")
            ?? throw new InvalidOperationException("Selection border is missing.");

        Position = virtualDesktopBounds.Position;
        Width = virtualDesktopBounds.Width / renderScaling;
        Height = virtualDesktopBounds.Height / renderScaling;
        _surface.PointerPressed += OnPointerPressed;
        _surface.PointerMoved += OnPointerMoved;
        _surface.PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_surface).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStart = e.GetPosition(_surface);
        _selection.IsVisible = true;
        UpdateSelection(_dragStart.Value, _dragStart.Value);
        e.Pointer.Capture(_surface);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        UpdateSelection(start, e.GetPosition(_surface));
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var end = e.GetPosition(_surface);
        e.Pointer.Capture(null);
        _dragStart = null;
        Close(RegionSelectionGeometry.ToCaptureRect(
            start,
            end,
            _virtualDesktopBounds.Position,
            _renderScaling));
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void UpdateSelection(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        Canvas.SetLeft(_selection, left);
        Canvas.SetTop(_selection, top);
        _selection.Width = Math.Abs(end.X - start.X);
        _selection.Height = Math.Abs(end.Y - start.Y);
    }
}
