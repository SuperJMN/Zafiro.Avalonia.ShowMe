using System.Reactive;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Viewport;
using Avalonia.Threading;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Services;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed class PreviewSessionViewModel : ReactiveObject, IDisposable
{
    private readonly PreviewServer server;
    private readonly IStorageService storageService;
    private FileSystemWatcher? fileWatcher;

    private WriteableBitmap? currentBitmap;
    private double previewWidth;
    private double previewHeight;
    private string zoomText = "100%";
    private bool isHotReloadActive = true;
    private string? xamlError;
    private string? xamlErrorDetails;
    private bool hasXamlError;
    private string currentTheme = XamlThemeModifier.ThemeDefault;
    private string status = "Iniciando sesión...";

    private bool isResizing;
    private bool hasUserSetDimensions;
    private CancellationTokenSource? dragDebounceCts;
    private DimensionPreset? selectedPreset;

    public event Action? RequestZoomIn;
    public event Action? RequestZoomOut;
    public event Action? RequestResetZoom;
    public event Action? RequestFit;

    public PreviewTarget Target { get; }

    public WriteableBitmap? CurrentBitmap
    {
        get => currentBitmap;
        private set => this.RaiseAndSetIfChanged(ref currentBitmap, value);
    }

    public double PreviewWidth
    {
        get => previewWidth;
        set
        {
            this.RaiseAndSetIfChanged(ref previewWidth, value);
            this.RaisePropertyChanged(nameof(DimensionsText));
        }
    }

    public double PreviewHeight
    {
        get => previewHeight;
        set
        {
            this.RaiseAndSetIfChanged(ref previewHeight, value);
            this.RaisePropertyChanged(nameof(DimensionsText));
        }
    }

    public bool IsResizing
    {
        get => isResizing;
        set => this.RaiseAndSetIfChanged(ref isResizing, value);
    }

    public string ZoomText
    {
        get => zoomText;
        set => this.RaiseAndSetIfChanged(ref zoomText, value);
    }

    public bool IsHotReloadActive
    {
        get => isHotReloadActive;
        set
        {
            this.RaiseAndSetIfChanged(ref isHotReloadActive, value);
            if (fileWatcher != null)
            {
                fileWatcher.EnableRaisingEvents = value;
            }
        }
    }

    public string? XamlError
    {
        get => xamlError;
        private set => this.RaiseAndSetIfChanged(ref xamlError, value);
    }

    public string? XamlErrorDetails
    {
        get => xamlErrorDetails;
        private set => this.RaiseAndSetIfChanged(ref xamlErrorDetails, value);
    }

    public bool HasXamlError
    {
        get => hasXamlError;
        private set => this.RaiseAndSetIfChanged(ref hasXamlError, value);
    }

    public string Status
    {
        get => status;
        private set => this.RaiseAndSetIfChanged(ref status, value);
    }

    public string DimensionsText => $"{PreviewWidth:F0} × {PreviewHeight:F0} px";

    public IReadOnlyList<DimensionPreset> Presets { get; }

    public DimensionPreset? SelectedPreset
    {
        get => selectedPreset;
        set
        {
            this.RaiseAndSetIfChanged(ref selectedPreset, value);
            if (value != null)
            {
                ApplyPreset(value);
            }
        }
    }

    public System.Windows.Input.ICommand SaveImageCommand { get; }
    public System.Windows.Input.ICommand CopyImageCommand { get; }
    public System.Windows.Input.ICommand ReloadCommand { get; }
    public System.Windows.Input.ICommand ZoomInCommand { get; }
    public System.Windows.Input.ICommand ZoomOutCommand { get; }
    public System.Windows.Input.ICommand ResetZoomCommand { get; }
    public System.Windows.Input.ICommand FitCommand { get; }
    public System.Windows.Input.ICommand ResetDimensionsCommand { get; }
    public System.Windows.Input.ICommand ApplyPresetCommand { get; }

    public PreviewSessionViewModel(
        PreviewTarget target,
        PreviewServer server,
        IStorageService storageService,
        string initialTheme = XamlThemeModifier.ThemeDefault,
        int? explicitWidth = null,
        int? explicitHeight = null)
    {
        Target = target;
        this.server = server;
        this.storageService = storageService;
        currentTheme = initialTheme;

        previewWidth = explicitWidth ?? target.InitialWidth ?? 1024;
        previewHeight = explicitHeight ?? target.InitialHeight ?? 768;
        if (explicitWidth.HasValue || explicitHeight.HasValue)
        {
            hasUserSetDimensions = true;
        }

        var origW = explicitWidth ?? target.InitialWidth ?? 800;
        var origH = explicitHeight ?? target.InitialHeight ?? 600;
        Presets = new List<DimensionPreset>
        {
            new("Diseño original", origW, origH),
            new("Ventana compacta", 800, 600),
            new("HD (720p)", 1280, 720),
            new("Full HD (1080p)", 1920, 1080),
            new("Tablet vertical", 768, 1024),
            new("Tablet horizontal", 1024, 768),
            new("Móvil vertical", 390, 844),
            new("Móvil horizontal", 844, 390),
        };

        SaveImageCommand = new AsyncDelegateCommand(SaveImageAsync);
        CopyImageCommand = new AsyncDelegateCommand(CopyImageAsync);
        ReloadCommand = new DelegateCommand(Reload);

        ZoomInCommand = new DelegateCommand(() => RequestZoomIn?.Invoke());
        ZoomOutCommand = new DelegateCommand(() => RequestZoomOut?.Invoke());
        ResetZoomCommand = new DelegateCommand(() => RequestResetZoom?.Invoke());
        FitCommand = new DelegateCommand(() => RequestFit?.Invoke());
        ResetDimensionsCommand = new DelegateCommand(ResetDimensions);
        ApplyPresetCommand = new DelegateCommand<DimensionPreset>(p =>
        {
            if (p != null)
            {
                ApplyPreset(p);
            }
        });

        server.FrameReceived += OnFrameReceived;
        server.XamlResultReceived += OnXamlResultReceived;
        server.StatusChanged += s => Dispatcher.UIThread.Post(() => Status = s);
        server.ErrorOccurred += err => Dispatcher.UIThread.Post(() =>
        {
            XamlError = err;
            HasXamlError = true;
        });

        SetupFileWatcher();
    }

    public void UpdateTheme(string theme)
    {
        currentTheme = theme;
        server.UpdateTheme(theme);
    }

    public void SetViewportSize(double width, double height)
    {
        SetCustomDimensions(width, height);
    }

    public void OnResizeDrag(double width, double height)
    {
        IsResizing = true;
        hasUserSetDimensions = true;
        PreviewWidth = Math.Round(Math.Clamp(width, 100, 4096));
        PreviewHeight = Math.Round(Math.Clamp(height, 100, 4096));

        dragDebounceCts?.Cancel();
        dragDebounceCts = new CancellationTokenSource();
        var token = dragDebounceCts.Token;

        Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => server.SetViewportSize(PreviewWidth, PreviewHeight));
            }
        }, TaskScheduler.Default);
    }

    public void OnResizeDragCompleted()
    {
        dragDebounceCts?.Cancel();
        IsResizing = false;
        hasUserSetDimensions = true;
        server.SetViewportSize(PreviewWidth, PreviewHeight);
    }

    public void SetCustomDimensions(double width, double height)
    {
        dragDebounceCts?.Cancel();
        hasUserSetDimensions = true;
        PreviewWidth = Math.Round(Math.Clamp(width, 100, 4096));
        PreviewHeight = Math.Round(Math.Clamp(height, 100, 4096));
        server.SetViewportSize(PreviewWidth, PreviewHeight);
    }

    public void ResetDimensions()
    {
        dragDebounceCts?.Cancel();
        hasUserSetDimensions = false;
        var targetW = Target.InitialWidth ?? 800;
        var targetH = Target.InitialHeight ?? 600;
        PreviewWidth = targetW;
        PreviewHeight = targetH;
        server.ResetViewportSize(targetW, targetH);
    }

    public void ApplyPreset(DimensionPreset preset)
    {
        SetCustomDimensions(preset.Width, preset.Height);
    }

    private void Reload()
    {
        try
        {
            if (File.Exists(Target.AxamlPath))
            {
                var content = File.ReadAllText(Target.AxamlPath);
                server.UpdateXaml(content, currentTheme);
                Status = "Recargando XAML...";
            }
        }
        catch (Exception ex)
        {
            XamlError = $"Error al leer el archivo AXAML: {ex.Message}";
            HasXamlError = true;
        }
    }

    private void OnFrameReceived(FrameMessage frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var pixelFormat = frame.Format == global::Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888
                    ? global::Avalonia.Platform.PixelFormat.Rgba8888
                    : global::Avalonia.Platform.PixelFormat.Bgra8888;

                var bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(frame.DpiX > 0 ? frame.DpiX : 96, frame.DpiY > 0 ? frame.DpiY : 96),
                    pixelFormat,
                    AlphaFormat.Premul);

                using (var locked = bitmap.Lock())
                {
                    Marshal.Copy(frame.Data, 0, locked.Address, frame.Data.Length);
                }

                CurrentBitmap = bitmap;
                if (!hasUserSetDimensions && !IsResizing)
                {
                    PreviewWidth = frame.Width;
                    PreviewHeight = frame.Height;
                    this.RaisePropertyChanged(nameof(DimensionsText));
                }
                HasXamlError = false;
                XamlError = null;
                XamlErrorDetails = null;
            }
            catch (Exception ex)
            {
                XamlError = $"Error al renderizar fotograma: {ex.Message}";
                HasXamlError = true;
            }
        });
    }

    private void OnXamlResultReceived(UpdateXamlResultMessage result)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!string.IsNullOrEmpty(result.Error) || result.Exception != null)
            {
                XamlError = result.Error ?? result.Exception?.Message ?? "Error desconocido en XAML";
                XamlErrorDetails = result.Exception != null
                    ? $"{result.Exception.ExceptionType} en línea {result.Exception.LineNumber}, pos {result.Exception.LinePosition}"
                    : null;
                HasXamlError = true;
            }
            else
            {
                HasXamlError = false;
                XamlError = null;
                XamlErrorDetails = null;
            }
        });
    }

    private void SetupFileWatcher()
    {
        try
        {
            var dir = Path.GetDirectoryName(Target.AxamlPath);
            var file = Path.GetFileName(Target.AxamlPath);

            if (dir != null && Directory.Exists(dir))
            {
                fileWatcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                // Debounce para evitar lecturas concurrentes al guardar
                var timer = new System.Timers.Timer(150) { AutoReset = false };
                timer.Elapsed += (_, _) => Dispatcher.UIThread.Post(Reload);

                fileWatcher.Changed += (_, _) =>
                {
                    if (IsHotReloadActive)
                    {
                        timer.Stop();
                        timer.Start();
                    }
                };
            }
        }
        catch
        {
            // Ignorar errores al iniciar FileSystemWatcher
        }
    }

    private async Task SaveImageAsync()
    {
        if (CurrentBitmap == null) return;

        var defaultName = $"Preview_{Target.TargetName}_{Path.GetFileNameWithoutExtension(Target.AxamlPath)}.png";
        var path = await storageService.SaveImageFileDialogAsync(defaultName);
        if (!string.IsNullOrWhiteSpace(path))
        {
            using var stream = File.Create(path);
#pragma warning disable CS0618
            CurrentBitmap.Save(stream);
#pragma warning restore CS0618
            Status = $"Imagen guardada en: {Path.GetFileName(path)}";
        }
    }

    private async Task CopyImageAsync()
    {
        if (CurrentBitmap == null) return;

        var success = await storageService.CopyBitmapToClipboardAsync(CurrentBitmap);
        if (success)
        {
            Status = "Imagen copiada al portapapeles.";
        }
    }

    public void Dispose()
    {
        fileWatcher?.Dispose();
        server.Dispose();
        CurrentBitmap?.Dispose();
    }
}
