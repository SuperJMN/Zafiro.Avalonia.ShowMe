using System.ComponentModel;
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
}
