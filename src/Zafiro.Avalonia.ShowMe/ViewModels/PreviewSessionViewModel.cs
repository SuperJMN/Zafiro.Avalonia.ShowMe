using System.Diagnostics;
using System.Reactive;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Protocol;
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

    private bool isInspectorActive;
    private ElementInspectionInfo? selectedElement;
    private Rect? selectionBounds;

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
        set
        {
            this.RaiseAndSetIfChanged(ref zoomText, value);
            if (ZoomLevel <= 0)
            {
                if (double.TryParse(value.TrimEnd('%'), out var parsed))
                {
                    ZoomLevel = parsed / 100.0;
                }
            }
        }
    }

    private double zoomLevel = 1.0;
    public double ZoomLevel
    {
        get => zoomLevel;
        set => this.RaiseAndSetIfChanged(ref zoomLevel, value);
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

    public bool IsInspectorActive
    {
        get => isInspectorActive;
        set
        {
            this.RaiseAndSetIfChanged(ref isInspectorActive, value);
            if (!value)
            {
                SelectedElement = null;
                SelectionBounds = null;
            }
        }
    }

    public ElementInspectionInfo? SelectedElement
    {
        get => selectedElement;
        private set => this.RaiseAndSetIfChanged(ref selectedElement, value);
    }

    public Rect? SelectionBounds
    {
        get => selectionBounds;
        private set => this.RaiseAndSetIfChanged(ref selectionBounds, value);
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
    public System.Windows.Input.ICommand ToggleInspectorCommand { get; }
    public System.Windows.Input.ICommand ClearSelectionCommand { get; }
    public System.Windows.Input.ICommand NavigateToCodeCommand { get; }

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

        ToggleInspectorCommand = new DelegateCommand(() => IsInspectorActive = !IsInspectorActive);
        ClearSelectionCommand = new DelegateCommand(() =>
        {
            SelectedElement = null;
            SelectionBounds = null;
        });
        NavigateToCodeCommand = new DelegateCommand(NavigateToCode);

        server.FrameReceived += OnFrameReceived;
        server.XamlStatusReceived += OnXamlStatusReceived;
        server.HitTestResultReceived += OnHitTestResultReceived;
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
                Dispatcher.UIThread.Post(() => server.SetViewportSize(PreviewWidth, PreviewHeight));
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

    public void OnPointerInput(PointerActionType action, Point pt, PointerMouseButton button, Vector delta, bool alt, bool ctrl, bool shift)
    {
        if (IsInspectorActive || ctrl)
        {
            if (action == PointerActionType.Down)
            {
                server.RequestHitTest(pt.X, pt.Y);
            }
        }
        else
        {
            server.SendPointerEvent(action, pt.X, pt.Y, button, delta.X, delta.Y, alt, ctrl, shift);
        }
    }

    public void ClearSelection()
    {
        SelectedElement = null;
        SelectionBounds = null;
    }

    public void OnKeyInput(KeyActionType action, int keyCode, string? text, bool alt, bool ctrl, bool shift)
    {
        if (!IsInspectorActive)
        {
            server.SendKeyEvent(action, keyCode, text, alt, ctrl, shift);
        }
    }

    public void NavigateToCode()
    {
        if (SelectedElement == null || SelectedElement.LineNumber <= 0) return;

        try
        {
            var line = SelectedElement.LineNumber;
            var col = SelectedElement.LinePosition;
            var file = Target.AxamlPath;

            var psi = new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"-g \"{file}:{line}:{col}\"",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{Target.AxamlPath}\"") { UseShellExecute = true });
            }
            catch { }
        }
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

    private void OnFrameReceived(ShowMeFramePacket frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    global::Avalonia.Platform.PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                using (var locked = bitmap.Lock())
                {
                    Marshal.Copy(frame.PixelData, 0, locked.Address, frame.PixelData.Length);
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

    private void OnXamlStatusReceived(XamlStatusMessage status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!status.Success)
            {
                XamlError = status.Error ?? "Error desconocido en XAML";
                XamlErrorDetails = status.LineNumber.HasValue
                    ? $"Línea {status.LineNumber}, pos {status.LinePosition}"
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

    private void OnHitTestResultReceived(HitTestResponseMessage hit)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (hit.Found)
            {
                var bounds = new Rect(hit.BoundsX, hit.BoundsY, hit.BoundsWidth, hit.BoundsHeight);
                SelectedElement = new ElementInspectionInfo(
                    TypeName: hit.TypeName,
                    ElementName: hit.ElementName,
                    LineNumber: hit.LineNumber,
                    LinePosition: hit.LinePosition,
                    SourceUri: hit.SourceUri,
                    Bounds: bounds,
                    Classes: hit.Classes ?? [],
                    Ancestors: hit.AncestorTree ?? [],
                    Properties: hit.Properties ?? new Dictionary<string, string>()
                );
                SelectionBounds = bounds;
            }
            else
            {
                SelectedElement = null;
                SelectionBounds = null;
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

                var timer = new System.Timers.Timer(150) { AutoReset = false };
                timer.Elapsed += (_, _) => Dispatcher.UIThread.Post(Reload);

                fileWatcher.Changed += (_, _) =>
                {
                    timer.Stop();
                    timer.Start();
                };
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error configurando FileSystemWatcher: {ex.Message}");
        }
    }

    private async Task SaveImageAsync()
    {
        if (CurrentBitmap == null) return;

        var defaultName = $"{Path.GetFileNameWithoutExtension(Target.AxamlPath)}_{PreviewWidth:F0}x{PreviewHeight:F0}.png";
        var filePath = await storageService.SaveImageFileDialogAsync(defaultName);

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                using var stream = File.Create(filePath);
                CurrentBitmap.Save(stream);
                Status = $"Imagen guardada en: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                XamlError = $"Error al guardar la imagen: {ex.Message}";
                HasXamlError = true;
            }
        }
    }

    private async Task CopyImageAsync()
    {
        if (CurrentBitmap == null) return;

        try
        {
            await storageService.CopyBitmapToClipboardAsync(CurrentBitmap);
            Status = "Imagen copiada al portapapeles.";
        }
        catch (Exception ex)
        {
            XamlError = $"Error al copiar imagen al portapapeles: {ex.Message}";
            HasXamlError = true;
        }
    }

    public void Dispose()
    {
        dragDebounceCts?.Dispose();
        fileWatcher?.Dispose();
        server.Dispose();
    }
}
