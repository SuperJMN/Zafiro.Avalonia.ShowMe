using Avalonia;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Metadata;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Views;

public enum ResizeDirection
{
    Horizontal,
    Vertical,
    Both
}

public class ResizeThumb : Thumb
{
    public static readonly StyledProperty<ResizeDirection> DirectionProperty =
        AvaloniaProperty.Register<ResizeThumb, ResizeDirection>(nameof(Direction));

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<ResizeThumb, object?>(nameof(Content));

    public static readonly StyledProperty<PreviewSessionViewModel?> SessionProperty =
        AvaloniaProperty.Register<ResizeThumb, PreviewSessionViewModel?>(nameof(Session));

    public ResizeDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public PreviewSessionViewModel? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    private Point? startPointerInParent;
    private double startWidth;
    private double startHeight;
    private double lastReportedWidth;
    private double lastReportedHeight;
    private Visual? parentReference;
    private ZoomBorder? zoomBorder;
    private PreviewSessionViewModel? explicitSession;

    public void BindSession(PreviewSessionViewModel? session)
    {
        explicitSession = session;
    }

    private PreviewSessionViewModel? ResolveSession() => Session ?? explicitSession ?? DataContext as PreviewSessionViewModel;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var session = ResolveSession();
        if (session == null)
        {
            return;
        }

        zoomBorder = this.FindAncestorOfType<ZoomBorder>();
        parentReference = zoomBorder ?? (Visual?)this.GetVisualParent();
        if (parentReference == null)
        {
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;

        startPointerInParent = e.GetPosition(parentReference);
        startWidth = session.PreviewWidth;
        startHeight = session.PreviewHeight;
        lastReportedWidth = startWidth;
        lastReportedHeight = startHeight;
        session.IsResizing = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (startPointerInParent == null || parentReference == null)
        {
            return;
        }

        var session = ResolveSession();
        if (session == null)
        {
            return;
        }

        e.Handled = true;

        var currentPos = e.GetPosition(parentReference);
        var zoomX = zoomBorder != null && zoomBorder.ZoomX > 0.001 ? zoomBorder.ZoomX : 1.0;
        var zoomY = zoomBorder != null && zoomBorder.ZoomY > 0.001 ? zoomBorder.ZoomY : 1.0;

        var totalDeltaX = (currentPos.X - startPointerInParent.Value.X) / zoomX;
        var totalDeltaY = (currentPos.Y - startPointerInParent.Value.Y) / zoomY;

        var newWidth = startWidth;
        var newHeight = startHeight;

        if (Direction is ResizeDirection.Horizontal or ResizeDirection.Both)
        {
            newWidth = Math.Round(Math.Clamp(startWidth + totalDeltaX, 100, 4096));
        }

        if (Direction is ResizeDirection.Vertical or ResizeDirection.Both)
        {
            newHeight = Math.Round(Math.Clamp(startHeight + totalDeltaY, 100, 4096));
        }

        var deltaW = newWidth - lastReportedWidth;
        var deltaH = newHeight - lastReportedHeight;

        if (deltaW != 0 || deltaH != 0)
        {
            lastReportedWidth = newWidth;
            lastReportedHeight = newHeight;

            if (zoomBorder != null)
            {
                var panDx = (deltaW / 2.0) * zoomX;
                var panDy = (deltaH / 2.0) * zoomY;
                zoomBorder.PanDelta(panDx, panDy, skipTransitions: true);
            }

            session.OnResizeDrag(newWidth, newHeight);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        e.Handled = true;
        CompleteResize();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CompleteResize();
    }

    private void CompleteResize()
    {
        if (startPointerInParent.HasValue)
        {
            startPointerInParent = null;
            parentReference = null;
            zoomBorder = null;
            ResolveSession()?.OnResizeDragCompleted();
        }
    }
}
