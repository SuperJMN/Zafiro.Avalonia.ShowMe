using System.ComponentModel;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Views;

public partial class MainWindow : Window
{
    private static readonly KeyGesture ToggleInspectorGesture = new(Key.C, KeyModifiers.Control | KeyModifiers.Shift);
    private static readonly KeyGesture GoBackGesture = new(Key.Left, KeyModifiers.Alt);
    private const double SourceViewerMinWidth = 280;
    private readonly SerialDisposable sessionSubscriptions = new();
    private GridLength sourceViewerWidth = new(480);

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

        if (CatalogImage != null)
        {
            CatalogImage.PointerPressed += OnPreviewPointerPressed;
            CatalogImage.PointerReleased += OnPreviewPointerReleased;
            CatalogImage.PointerMoved += OnPreviewPointerMoved;
            CatalogImage.PointerExited += OnPreviewPointerExited;
        }

        CatalogScrollViewer?.GetObservable(Visual.BoundsProperty).Subscribe(bounds =>
        {
            if (bounds.Width > 0 && DataContext is MainViewModel { CurrentSession: { IsCatalogViewVisible: true } session })
            {
                session.OnCatalogViewportWidthChanged(bounds.Width);
            }
        });

        SourceViewerPanel?.GetObservable(IsVisibleProperty).Subscribe(UpdateSourceViewerColumn);

        DataContextChanged += OnDataContextChanged;
    }

    // El GridSplitter fija la columna del visor en píxeles; al cerrarlo se guarda ese ancho y se pliega
    // la columna para que no deje un hueco vacío, y al abrirlo de nuevo se restaura.
    private void UpdateSourceViewerColumn(bool isVisible)
    {
        if (PreviewArea == null) return;

        var column = PreviewArea.ColumnDefinitions[2];

        if (isVisible)
        {
            column.MinWidth = SourceViewerMinWidth;
            column.Width = sourceViewerWidth;
            return;
        }

        if (column.Width.IsAbsolute)
        {
            sourceViewerWidth = column.Width;
        }

        column.MinWidth = 0;
        column.Width = GridLength.Auto;
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
        sessionSubscriptions.Disposable = null;

        RightThumb?.BindSession(session);
        BottomThumb?.BindSession(session);
        CornerThumb?.BindSession(session);

        if (session == null) return;

        var subscriptions = new CompositeDisposable();

        Action<Point, IReadOnlyList<InspectMenuItemViewModel>> showContextMenu = (point, items) =>
        {
            ShowInspectContextMenu(point, items, session);
        };
        session.RequestShowContextMenu += showContextMenu;
        subscriptions.Add(Disposable.Create(() => session.RequestShowContextMenu -= showContextMenu));

        Action zoomIn = () =>
        {
            ZoomBorder?.ZoomIn();
            UpdateZoomText(session);
        };
        session.RequestZoomIn += zoomIn;
        subscriptions.Add(Disposable.Create(() => session.RequestZoomIn -= zoomIn));

        Action zoomOut = () =>
        {
            ZoomBorder?.ZoomOut();
            UpdateZoomText(session);
        };
        session.RequestZoomOut += zoomOut;
        subscriptions.Add(Disposable.Create(() => session.RequestZoomOut -= zoomOut));

        Action resetZoom = () =>
        {
            ZoomBorder?.ResetMatrix();
            UpdateZoomText(session);
        };
        session.RequestResetZoom += resetZoom;
        subscriptions.Add(Disposable.Create(() => session.RequestResetZoom -= resetZoom));

        Action fit = () =>
        {
            ZoomBorder?.AutoFit();
            UpdateZoomText(session);
        };
        session.RequestFit += fit;
        subscriptions.Add(Disposable.Create(() => session.RequestFit -= fit));

        bool initialFitDone = false;
        void FitOnFirstFrame()
        {
            if (initialFitDone || session.CurrentBitmap == null)
            {
                return;
            }

            if (session.IsCatalogActive && !session.IsPreviewWithMode)
            {
                return;
            }

            initialFitDone = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ZoomBorder?.AutoFit();
                UpdateZoomText(session);
            }, DispatcherPriority.Loaded);
        }

        PropertyChangedEventHandler sessionPropertyChanged = (s, e) =>
        {
            if (e.PropertyName == nameof(PreviewSessionViewModel.IsCatalogViewVisible))
            {
                if (session.IsCatalogViewVisible && CatalogScrollViewer != null && CatalogScrollViewer.Bounds.Width > 0)
                {
                    session.OnCatalogViewportWidthChanged(CatalogScrollViewer.Bounds.Width);
                }
            }
            else if (e.PropertyName == nameof(PreviewSessionViewModel.CurrentBitmap))
            {
                FitOnFirstFrame();
            }
        };
        session.PropertyChanged += sessionPropertyChanged;
        subscriptions.Add(Disposable.Create(() => session.PropertyChanged -= sessionPropertyChanged));

        // La sesión se publica cuando el previewer ya ha respondido, así que su primer fotograma puede haber llegado antes.
        FitOnFirstFrame();

        if (ZoomBorder is { } zoomBorder)
        {
            ZoomChangedEventHandler zoomChanged = (_, _) =>
            {
                UpdateZoomText(session);
            };
            zoomBorder.ZoomChanged += zoomChanged;
            subscriptions.Add(Disposable.Create(() => zoomBorder.ZoomChanged -= zoomChanged));
        }

        sessionSubscriptions.Disposable = subscriptions;
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

        var visualTarget = sender as Visual ?? PreviewImage;
        (visualTarget as InputElement)?.Focus();

        var currentPoint = e.GetCurrentPoint(visualTarget);
        var pt = currentPoint.Position;
        var props = currentPoint.Properties;

        if (props.IsRightButtonPressed)
        {
            session.RequestInspectContextMenu(pt);
            e.Handled = true;
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

        session.OnPointerInput(PointerActionType.Down, pt, btn, default, alt, ctrl, shift, e.ClickCount);
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        var visualTarget = sender as Visual ?? PreviewImage;
        var currentPoint = e.GetCurrentPoint(visualTarget);
        var pt = currentPoint.Position;
        var props = currentPoint.Properties;

        if (props.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
        {
            e.Handled = true;
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

        var visualTarget = sender as Visual ?? PreviewImage;
        var currentPoint = e.GetCurrentPoint(visualTarget);
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

        // CanExecute is false while a previous "Atrás" is still loading, like the button.
        if (GoBackGesture.Matches(e) && DataContext is MainViewModel main && main.GoBackCommand.CanExecute(null))
        {
            main.GoBackCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (DataContext is not MainViewModel { CurrentSession: { } session }) return;

        if (e.Key == Key.Escape && session.HandleEscape())
        {
            e.Handled = true;
            return;
        }

        if (ToggleInspectorGesture.Matches(e))
        {
            session.ToggleInspectorCommand.Execute(null);
            e.Handled = true;
            return;
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
                Command = item.SelectCommand
            };

            menuItem.Items.Add(new MenuItem { Header = "Ver XAML", Command = item.ViewXamlCommand });
            if (item.CanOpenView)
            {
                menuItem.Items.Add(new MenuItem { Header = "Abrir vista", Command = item.OpenViewCommand });
            }
            if (item.CanOpenInEditor)
            {
                menuItem.Items.Add(new MenuItem { Header = "Abrir en el editor", Command = item.OpenInEditorCommand });
            }

            // A mouse click on an item with a submenu only opens the submenu and never runs its Command,
            // so releasing the button on the item's own row selects the element too.
            menuItem.AddHandler(PointerReleasedEvent, (_, e) =>
            {
                var clicked = (e.Source as StyledElement)?.FindLogicalAncestorOfType<MenuItem>(includeSelf: true);
                if (clicked != menuItem || e.InitialPressMouseButton != MouseButton.Left)
                {
                    return;
                }

                item.SelectCommand.Execute(null);
                menu.Close();
                e.Handled = true;
            }, RoutingStrategies.Bubble, handledEventsToo: true);

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
