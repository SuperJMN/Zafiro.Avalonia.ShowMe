using Avalonia;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Zafiro.Avalonia.ShowMe.ViewModels;

using Avalonia.Metadata;

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

    private Point? startPointerInParent;
    private double startWidth;
    private double startHeight;
    private Visual? parentReference;
    private PreviewSessionViewModel? explicitSession;

    public void BindSession(PreviewSessionViewModel? session)
    {
        explicitSession = session;
    }

    private PreviewSessionViewModel? Session => explicitSession ?? DataContext as PreviewSessionViewModel;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var session = Session;
        if (session == null)
        {
            return;
        }

        var zoomBorder = this.FindAncestorOfType<ZoomBorder>();
        parentReference = zoomBorder ?? (Visual?)this.GetVisualParent();
        if (parentReference == null)
        {
            return;
        }

        startPointerInParent = e.GetPosition(parentReference);
        startWidth = session.PreviewWidth;
        startHeight = session.PreviewHeight;
        session.IsResizing = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (startPointerInParent == null || parentReference == null)
        {
            return;
        }

        var session = Session;
        if (session == null)
        {
            return;
        }

        var currentPos = e.GetPosition(parentReference);
        var zoomBorder = parentReference as ZoomBorder;
        var zoomX = zoomBorder != null && zoomBorder.ZoomX > 0.001 ? zoomBorder.ZoomX : 1.0;
        var zoomY = zoomBorder != null && zoomBorder.ZoomY > 0.001 ? zoomBorder.ZoomY : 1.0;

        var totalDeltaX = (currentPos.X - startPointerInParent.Value.X) / zoomX;
        var totalDeltaY = (currentPos.Y - startPointerInParent.Value.Y) / zoomY;

        var newWidth = startWidth;
        var newHeight = startHeight;

        if (Direction is ResizeDirection.Horizontal or ResizeDirection.Both)
        {
            newWidth = Math.Max(100, startWidth + totalDeltaX);
        }

        if (Direction is ResizeDirection.Vertical or ResizeDirection.Both)
        {
            newHeight = Math.Max(100, startHeight + totalDeltaY);
        }

        session.OnResizeDrag(newWidth, newHeight);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
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
            Session?.OnResizeDragCompleted();
        }
    }
}
