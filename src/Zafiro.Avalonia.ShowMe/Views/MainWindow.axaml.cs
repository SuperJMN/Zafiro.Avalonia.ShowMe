using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        if (PreviewImage != null)
        {
            PreviewImage.PointerPressed += OnPreviewPointerPressed;
            PreviewImage.PointerReleased += OnPreviewPointerReleased;
            PreviewImage.PointerMoved += OnPreviewPointerMoved;
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnMainViewModelPropertyChanged;
            HookSession(vm.CurrentSession);
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentSession) && DataContext is MainViewModel vm)
        {
            HookSession(vm.CurrentSession);
        }
    }

    private void HookSession(PreviewSessionViewModel? session)
    {
        if (session == null) return;

        RightThumb?.BindSession(session);
        BottomThumb?.BindSession(session);
        CornerThumb?.BindSession(session);

        session.RequestZoomIn += () =>
        {
            ZoomBorder?.ZoomIn();
            UpdateZoomText(session);
        };

        session.RequestZoomOut += () =>
        {
            ZoomBorder?.ZoomOut();
            UpdateZoomText(session);
        };

        session.RequestResetZoom += () =>
        {
            ZoomBorder?.ResetMatrix();
            UpdateZoomText(session);
        };

        session.RequestFit += () =>
        {
            ZoomBorder?.AutoFit();
            UpdateZoomText(session);
        };

        bool initialFitDone = false;
        session.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PreviewSessionViewModel.CurrentBitmap) && !initialFitDone && session.CurrentBitmap != null)
            {
                initialFitDone = true;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ZoomBorder?.AutoFit();
                    UpdateZoomText(session);
                }, DispatcherPriority.Loaded);
            }
        };

        if (ZoomBorder != null)
        {
            ZoomBorder.ZoomChanged += (_, _) =>
            {
                UpdateZoomText(session);
            };
        }
    }

    private void UpdateZoomText(PreviewSessionViewModel session)
    {
        if (ZoomBorder != null)
        {
            var zoom = (int)Math.Round(ZoomBorder.ZoomX * 100);
            session.ZoomText = $"{zoom}%";
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
        {
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files == null)
        {
            return;
        }

        foreach (var item in files)
        {
            if (item is IStorageFile file)
            {
                var path = file.Path.LocalPath;
                if (!string.IsNullOrWhiteSpace(path) &&
                    (path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
                {
                    if (DataContext is MainViewModel vm)
                    {
                        await vm.LoadFileAsync(path);
                        break;
                    }
                }
            }
        }
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        PreviewImage?.Focus();

        var currentPoint = e.GetCurrentPoint(PreviewImage);
        var pt = currentPoint.Position;
        var props = currentPoint.Properties;

        if (props.IsRightButtonPressed)
        {
            return;
        }

        var btn = props.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => PointerMouseButton.Left,
            PointerUpdateKind.MiddleButtonPressed => PointerMouseButton.Middle,
            _ => props.IsLeftButtonPressed ? PointerMouseButton.Left : PointerMouseButton.None
        };

        var keyMods = e.KeyModifiers;
        bool alt = keyMods.HasFlag(KeyModifiers.Alt);
        bool ctrl = keyMods.HasFlag(KeyModifiers.Control);
        bool shift = keyMods.HasFlag(KeyModifiers.Shift);

        session.OnPointerInput(PointerActionType.Down, pt, btn, default, alt, ctrl, shift);
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        var currentPoint = e.GetCurrentPoint(PreviewImage);
        var pt = currentPoint.Position;
        var props = currentPoint.Properties;

        if (props.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
        {
            return;
        }

        var btn = props.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonReleased => PointerMouseButton.Left,
            PointerUpdateKind.MiddleButtonReleased => PointerMouseButton.Middle,
            _ => PointerMouseButton.Left
        };

        var keyMods = e.KeyModifiers;
        bool alt = keyMods.HasFlag(KeyModifiers.Alt);
        bool ctrl = keyMods.HasFlag(KeyModifiers.Control);
        bool shift = keyMods.HasFlag(KeyModifiers.Shift);

        session.OnPointerInput(PointerActionType.Up, pt, btn, default, alt, ctrl, shift);
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        var currentPoint = e.GetCurrentPoint(PreviewImage);
        var pt = currentPoint.Position;
        var props = currentPoint.Properties;

        var btn = props.IsLeftButtonPressed ? PointerMouseButton.Left :
                  props.IsMiddleButtonPressed ? PointerMouseButton.Middle : PointerMouseButton.None;

        var keyMods = e.KeyModifiers;
        bool alt = keyMods.HasFlag(KeyModifiers.Alt);
        bool ctrl = keyMods.HasFlag(KeyModifiers.Control);
        bool shift = keyMods.HasFlag(KeyModifiers.Shift);

        session.OnPointerInput(PointerActionType.Move, pt, btn, default, alt, ctrl, shift);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        if (e.Key == Key.Escape)
        {
            if (session.SelectedElement != null)
            {
                session.ClearSelection();
                e.Handled = true;
                return;
            }
        }

        var focused = FocusManager?.GetFocusedElement();
        if (focused is not null && focused != this && focused != ZoomBorder && focused != PreviewImage)
        {
            return;
        }

        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Don't forward shortcuts like Ctrl+O
        if (!ctrl || e.Key is Key.C or Key.V or Key.A or Key.X or Key.Z or Key.Y)
        {
            session.OnKeyInput(KeyActionType.Down, (int)e.Key, null, alt, ctrl, shift);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        var focused = FocusManager?.GetFocusedElement();
        if (focused is not null && focused != this && focused != ZoomBorder && focused != PreviewImage)
        {
            return;
        }

        if (!string.IsNullOrEmpty(e.Text))
        {
            session.OnKeyInput(KeyActionType.TextInput, 0, e.Text, false, false, false);
        }
    }
}
