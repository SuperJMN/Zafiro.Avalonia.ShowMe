using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
                    AdjustWindowToContent(session);
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

    private void AdjustWindowToContent(PreviewSessionViewModel session)
    {
        if (WindowState is WindowState.Maximized or WindowState.FullScreen)
        {
            ZoomBorder?.AutoFit();
            UpdateZoomText(session);
            return;
        }

        var contentWidth = session.PreviewWidth;
        var contentHeight = session.PreviewHeight;

        if (contentWidth <= 0 || contentHeight <= 0 || double.IsNaN(contentWidth) || double.IsNaN(contentHeight))
        {
            ZoomBorder?.AutoFit();
            UpdateZoomText(session);
            return;
        }

        // Obtener el área de trabajo de la pantalla actual en DIPs
        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        var scaling = screen?.Scaling ?? 1.0;
        var maxWorkWidth = screen != null ? (screen.WorkingArea.Width / scaling) - 40 : 1920;
        var maxWorkHeight = screen != null ? (screen.WorkingArea.Height / scaling) - 60 : 1080;

        // Márgenes necesarios para marco de previsualización, thumbs y barras superior/inferior
        // Horizontal: right thumb (14px) + márgenes laterales en el lienzo (50px) = ~64px
        // Vertical: toolbar (~48px) + statusbar (~28px) + preview header (~28px) + bottom thumb (~14px) + márgenes en lienzo (40px) = ~158px
        const double extraWidth = 64;
        const double extraHeight = 158;

        const double minWindowWidth = 1000;
        const double minWindowHeight = 450;

        var targetWidth = Math.Max(minWindowWidth, contentWidth + extraWidth);
        var targetHeight = Math.Max(minWindowHeight, contentHeight + extraHeight);

        var clampedWidth = Math.Min(targetWidth, maxWorkWidth);
        var clampedHeight = Math.Min(targetHeight, maxWorkHeight);

        Width = clampedWidth;
        Height = clampedHeight;

        // Re-centrar ventana manteniendo el centro actual en la pantalla
        if (screen != null && Bounds.Width > 0 && Bounds.Height > 0)
        {
            try
            {
                var currentCenterX = Position.X + (int)Math.Round(Bounds.Width * scaling / 2.0);
                var currentCenterY = Position.Y + (int)Math.Round(Bounds.Height * scaling / 2.0);

                var newX = currentCenterX - (int)Math.Round(clampedWidth * scaling / 2.0);
                var newY = currentCenterY - (int)Math.Round(clampedHeight * scaling / 2.0);

                var wa = screen.WorkingArea;
                var maxX = wa.Right - (int)Math.Round(clampedWidth * scaling);
                var maxY = wa.Bottom - (int)Math.Round(clampedHeight * scaling);

                if (maxX >= wa.X)
                {
                    newX = Math.Clamp(newX, wa.X, maxX);
                }
                if (maxY >= wa.Y)
                {
                    newY = Math.Clamp(newY, wa.Y, maxY);
                }

                Position = new PixelPoint(newX, newY);
            }
            catch
            {
                // En Wayland o algunos compositores el posicionamiento manual de ventanas no es soportado
            }
        }

        var contentExceedsWindow = (contentWidth + extraWidth > clampedWidth) || (contentHeight + extraHeight > clampedHeight);

        Dispatcher.UIThread.Post(() =>
        {
            if (contentExceedsWindow)
            {
                ZoomBorder?.AutoFit();
            }
            else
            {
                ZoomBorder?.ResetMatrix();
            }
            UpdateZoomText(session);
        }, DispatcherPriority.Loaded);
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
}
