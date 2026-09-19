using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
            PreviewImage.PointerExited += OnPreviewPointerExited;
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

        session.RequestShowContextMenu += (point, items) =>
        {
            ShowInspectContextMenu(point, items, session);
        };

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
            if (session.IsInspectorActive || e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                session.RequestInspectContextMenu(pt);
                e.Handled = true;
            }
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
        if (e.ClickCount >= 2 && (session.IsInspectorActive || ctrl))
        {
            session.NavigateToCode();
        }

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
            if (session.IsInspectorActive || e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
            }
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

    private void OnPreviewPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;
        session.ClearHover();
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

    private void ShowInspectContextMenu(Point point, IReadOnlyList<InspectMenuItemViewModel> items, PreviewSessionViewModel session)
    {
        if (items.Count == 0 || PreviewImage == null) return;

        var menu = new ContextMenu();
        foreach (var item in items)
        {
            var menuItem = new MenuItem
            {
                Header = CreateInspectMenuItemHeader(item),
                Command = item.NavigateCommand
            };

            menuItem.PointerEntered += (_, _) =>
            {
                session.HoverElement(item);
            };

            menu.Items.Add(menuItem);
        }

        menu.Closed += (_, _) =>
        {
            session.ClearHover();
        };

        menu.Placement = PlacementMode.Bottom;
        menu.PlacementRect = new Rect(point.X, point.Y, 1, 1);
        menu.PlacementTarget = PreviewImage;
        menu.Open(PreviewImage);
    }

    private static Control CreateInspectMenuItemHeader(InspectMenuItemViewModel item)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = item.TypeName,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrWhiteSpace(item.ElementName))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"#{item.ElementName}",
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8")),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (!string.IsNullOrWhiteSpace(item.RelativeFilePath))
        {
            panel.Children.Add(new TextBlock
            {
                Text = item.RelativeFilePath,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (!string.IsNullOrWhiteSpace(item.LocationText))
        {
            panel.Children.Add(new TextBlock
            {
                Text = item.LocationText,
                Foreground = new SolidColorBrush(Color.Parse("#64748B")),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return panel;
    }
}
