using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.UI.Commands;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed class PreviewSessionViewModel : ReactiveObject, IDisposable
{
    private readonly PreviewServer server;
    private readonly IStorageService storageService;
    private readonly Maybe<CodeEditor> codeEditor;
    private readonly Func<string, Task>? openView;
    private readonly IDisposable codeEditorSubscription;
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
    private bool isInspectionPress;
    private ElementInspectionInfo? selectedElement;
    private Rect? selectionBounds;
    private ElementInspectionInfo? hoveredElement;
    private XamlSourceViewModel? sourceView;
    private bool canOpenInEditor;
    private bool viewXamlOnNextSelection;
    private bool canOpenDefiningView;
    private ElementInspectionInfo? selectionToRestore;
    private readonly BehaviorSubject<bool> definingViewAvailable = new(false);
    private readonly BehaviorSubject<bool> hasSelection = new(false);
    private readonly BehaviorSubject<bool> editorAvailable = new(false);

    private bool isHoverRequestPending;
    private Point? pendingHoverPoint;
    private long lastHoverRequestTimestamp;

    public event Action? RequestZoomIn;
    public event Action? RequestZoomOut;
    public event Action? RequestResetZoom;
    public event Action? RequestFit;
    public event Action<Point, IReadOnlyList<InspectMenuItemViewModel>>? RequestShowContextMenu;
    public Action<Action> DispatchToUI { get; set; } = action => Dispatcher.UIThread.Post(action);

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
                ClearHover();
            }
        }
    }

    public ElementInspectionInfo? SelectedElement
    {
        get => selectedElement;
        private set
        {
            this.RaiseAndSetIfChanged(ref selectedElement, value);
            this.RaisePropertyChanged(nameof(IsHoverAdornerVisible));
            hasSelection.OnNext(value != null);
            CanOpenDefiningView = value != null && CanOpenView(SelectedFile(value));
        }
    }

    public ElementInspectionInfo? HoveredElement
    {
        get => hoveredElement;
        private set
        {
            this.RaiseAndSetIfChanged(ref hoveredElement, value);
            this.RaisePropertyChanged(nameof(IsHoverAdornerVisible));
        }
    }

    public bool IsHoverAdornerVisible =>
        HoveredElement != null &&
        (SelectedElement == null || SelectedElement.Bounds != HoveredElement.Bounds);

    /// <summary>
    /// The XAML shown in the read-only viewer, or null when the viewer is closed.
    /// </summary>
    public XamlSourceViewModel? SourceView
    {
        get => sourceView;
        private set => this.RaiseAndSetIfChanged(ref sourceView, value);
    }

    /// <summary>
    /// True when an external editor is configured, so "Abrir en el editor" is offered.
    /// </summary>
    public bool CanOpenInEditor
    {
        get => canOpenInEditor;
        private set
        {
            this.RaiseAndSetIfChanged(ref canOpenInEditor, value);
            editorAvailable.OnNext(value);
        }
    }

    /// <summary>
    /// True when the selected element is defined in another file that ShowMe can preview, so "Abrir vista" is offered.
    /// </summary>
    public bool CanOpenDefiningView
    {
        get => canOpenDefiningView;
        private set
        {
            this.RaiseAndSetIfChanged(ref canOpenDefiningView, value);
            definingViewAvailable.OnNext(value);
        }
    }

    /// <summary>
    /// The size the user chose for this view, or null when it still uses the size of its design.
    /// </summary>
    public PixelSize? ChosenSize => hasUserSetDimensions ? new PixelSize((int)PreviewWidth, (int)PreviewHeight) : null;

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

    // Catálogo de Recursos (Shell nativa)
    private bool isCatalogActive;
    private bool isSidebarVisible = true;
    private string catalogTitle = "Catálogo de Recursos";
    private int catalogItemCount;
    private bool hasPreviewWith;
    private string sidebarMode = "Tree"; // "Tree" | "Flat" | "PreviewWith"
    private string catalogSearchText = "";
    private object? selectedCatalogNode;

    public bool IsCatalogActive
    {
        get => isCatalogActive;
        private set
        {
            this.RaiseAndSetIfChanged(ref isCatalogActive, value);
            this.RaisePropertyChanged(nameof(IsSidebarVisible));
            this.RaisePropertyChanged(nameof(IsCatalogViewVisible));
            this.RaisePropertyChanged(nameof(IsControlPreviewVisible));
        }
    }

    public bool IsSidebarVisible
    {
        get => isSidebarVisible && isCatalogActive;
        set
        {
            this.RaiseAndSetIfChanged(ref isSidebarVisible, value);
            this.RaisePropertyChanged(nameof(IsSidebarVisible));
        }
    }

    public string CatalogTitle
    {
        get => catalogTitle;
        private set => this.RaiseAndSetIfChanged(ref catalogTitle, value);
    }

    public int CatalogItemCount
    {
        get => catalogItemCount;
        private set => this.RaiseAndSetIfChanged(ref catalogItemCount, value);
    }

    public bool HasPreviewWith
    {
        get => hasPreviewWith;
        private set => this.RaiseAndSetIfChanged(ref hasPreviewWith, value);
    }

    public bool IsTreeMode => SidebarMode == "Tree";
    public bool IsFlatMode => SidebarMode == "Flat";
    public bool IsPreviewWithMode => SidebarMode == "PreviewWith";

    public bool IsCatalogViewVisible => IsCatalogActive && !IsPreviewWithMode;
    public bool IsControlPreviewVisible => !IsCatalogActive || IsPreviewWithMode;

    public void OnCatalogViewportWidthChanged(double width)
    {
        if (!IsCatalogViewVisible) return;

        var targetW = Math.Round(Math.Clamp(width - 24, 400, 4096));
        if (Math.Abs(targetW - PreviewWidth) > 30)
        {
            dragDebounceCts?.Cancel();
            dragDebounceCts = new CancellationTokenSource();
            var token = dragDebounceCts.Token;
            Task.Delay(150, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        server.SetViewportSize(targetW, PreviewHeight);
                    });
                }
            }, TaskScheduler.Default);
        }
    }

    public string SidebarMode
    {
        get => sidebarMode;
        set
        {
            this.RaiseAndSetIfChanged(ref sidebarMode, value);
            this.RaisePropertyChanged(nameof(IsTreeMode));
            this.RaisePropertyChanged(nameof(IsFlatMode));
            this.RaisePropertyChanged(nameof(IsPreviewWithMode));
            this.RaisePropertyChanged(nameof(IsCatalogViewVisible));
            this.RaisePropertyChanged(nameof(IsControlPreviewVisible));
            if (value == "PreviewWith")
            {
                server.SelectCatalogItem(null, "PreviewWith");
            }
            else if (selectedCatalogNode == null)
            {
                server.SelectCatalogItem(null, "All", catalogSearchText);
            }
        }
    }

    public string CatalogSearchText
    {
        get => catalogSearchText;
        set
        {
            this.RaiseAndSetIfChanged(ref catalogSearchText, value);
            ApplyCatalogFilter(value);
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<CatalogGroupNodeViewModel> CatalogTreeNodes { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<CatalogItemViewModel> CatalogFlatItems { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<CatalogItemViewModel> FilteredFlatItems { get; } = new();

    public object? SelectedCatalogNode
    {
        get => selectedCatalogNode;
        set
        {
            this.RaiseAndSetIfChanged(ref selectedCatalogNode, value);
            OnCatalogSelectionChanged(value);
        }
    }

    public IEnhancedCommand ToggleSidebarCommand { get; }
    public IEnhancedCommand SetTreeModeCommand { get; }
    public IEnhancedCommand SetFlatModeCommand { get; }
    public IEnhancedCommand SelectAllCardsCommand { get; }
    public IEnhancedCommand SelectPreviewWithCommand { get; }
    public IEnhancedCommand SaveImageCommand { get; }
    public IEnhancedCommand CopyImageCommand { get; }
    public IEnhancedCommand ReloadCommand { get; }
    public IEnhancedCommand ZoomInCommand { get; }
    public IEnhancedCommand ZoomOutCommand { get; }
    public IEnhancedCommand ResetZoomCommand { get; }
    public IEnhancedCommand FitCommand { get; }
    public IEnhancedCommand ResetDimensionsCommand { get; }
    public IEnhancedCommand ToggleInspectorCommand { get; }
    public IEnhancedCommand ViewXamlCommand { get; }
    public IEnhancedCommand OpenInEditorCommand { get; }
    public IEnhancedCommand OpenDefiningViewCommand { get; }
    public IEnhancedCommand CloseSourceViewCommand { get; }

    public PreviewSessionViewModel(
        PreviewTarget target,
        PreviewServer server,
        IStorageService storageService,
        string initialTheme = XamlThemeModifier.ThemeDefault,
        int? explicitWidth = null,
        int? explicitHeight = null,
        CodeEditor? codeEditor = null,
        Func<string, Task>? openView = null,
        PixelSize? restoredSize = null)
    {
        Target = target;
        this.server = server;
        this.storageService = storageService;
        this.codeEditor = Maybe.From(codeEditor);
        this.openView = openView;
        currentTheme = initialTheme;

        // The size the user chose before navigating away wins over the one given on the command line, but only the
        // latter, or the design, defines "Diseño original".
        previewWidth = restoredSize?.Width ?? explicitWidth ?? target.InitialWidth ?? 1024;
        previewHeight = restoredSize?.Height ?? explicitHeight ?? target.InitialHeight ?? 768;
        if (restoredSize.HasValue || explicitWidth.HasValue || explicitHeight.HasValue)
        {
            hasUserSetDimensions = true;
            // Before the server starts this only sets the size of the first render.
            server.SetViewportSize(previewWidth, previewHeight);
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

        SaveImageCommand = EnhancedCommand.Create(SaveImageAsync);
        CopyImageCommand = EnhancedCommand.Create(CopyImageAsync);
        ReloadCommand = EnhancedCommand.Create(Reload);

        ZoomInCommand = EnhancedCommand.Create(() => RequestZoomIn?.Invoke());
        ZoomOutCommand = EnhancedCommand.Create(() => RequestZoomOut?.Invoke());
        ResetZoomCommand = EnhancedCommand.Create(() => RequestResetZoom?.Invoke());
        FitCommand = EnhancedCommand.Create(() => RequestFit?.Invoke());
        ResetDimensionsCommand = EnhancedCommand.Create(ResetDimensions);
        ToggleInspectorCommand = EnhancedCommand.Create(() => IsInspectorActive = !IsInspectorActive);
        ToggleSidebarCommand = EnhancedCommand.Create(() => IsSidebarVisible = !IsSidebarVisible);
        SetTreeModeCommand = EnhancedCommand.Create(() => SidebarMode = "Tree");
        SetFlatModeCommand = EnhancedCommand.Create(() => SidebarMode = "Flat");
        SelectAllCardsCommand = EnhancedCommand.Create(() =>
        {
            SelectedCatalogNode = null;
            server.SelectCatalogItem(null, "All", CatalogSearchText);
        });
        SelectPreviewWithCommand = EnhancedCommand.Create(() =>
        {
            SelectedCatalogNode = null;
            SidebarMode = "PreviewWith";
            server.SelectCatalogItem(null, "PreviewWith");
        });
        ViewXamlCommand = EnhancedCommand.Create(ViewSelectedXaml, hasSelection);
        OpenInEditorCommand = EnhancedCommand.Create(
            OpenSelectedInEditor,
            hasSelection.CombineLatest(editorAvailable, (selected, available) => selected && available));
        OpenDefiningViewCommand = EnhancedCommand.Create(OpenDefiningViewAsync, definingViewAvailable);
        CloseSourceViewCommand = EnhancedCommand.Create(() => SourceView = null);
        codeEditorSubscription = this.codeEditor.Match(
            editor =>
            {
                CanOpenInEditor = editor.IsAvailable;
                return Observable.FromEventPattern<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
                        h => editor.PropertyChanged += h,
                        h => editor.PropertyChanged -= h)
                    .Where(e => e.EventArgs.PropertyName == nameof(CodeEditor.IsAvailable))
                    .Subscribe(_ => CanOpenInEditor = editor.IsAvailable);
            },
            () => System.Reactive.Disposables.Disposable.Empty);

        server.FrameReceived += OnFrameReceived;
        server.XamlStatusReceived += OnXamlStatusReceived;
        server.HitTestResultReceived += OnHitTestResultReceived;
        server.ContextMenuHitTestResultReceived += OnContextMenuHitTestResultReceived;
        server.FindElementResultReceived += OnFindElementResultReceived;
        server.CatalogInfoReceived += OnCatalogInfoReceived;
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

    public void OnPointerInput(PointerActionType action, Point pt, PointerMouseButton button, Vector delta, bool alt, bool ctrl, bool shift, int clickCount = 1)
    {
        if (action == PointerActionType.Down)
        {
            var isSecondClickOfInspection = clickCount >= 2 && isInspectionPress;
            isInspectionPress = IsInspectorActive || ctrl || isSecondClickOfInspection;

            if (isSecondClickOfInspection)
            {
                // The double-clicked element may not be selected yet, so its XAML is shown when the hit test answers.
                viewXamlOnNextSelection = true;
                server.RequestHitTest(pt.X, pt.Y, isHover: false);
                return;
            }

            if (isInspectionPress)
            {
                server.RequestHitTest(pt.X, pt.Y, isHover: false);
                IsInspectorActive = false;
                return;
            }
        }
        else if (action == PointerActionType.Up && isInspectionPress)
        {
            return;
        }
        else if (action == PointerActionType.Move && (IsInspectorActive || ctrl))
        {
            RequestHoverHitTest(pt);
            return;
        }

        ClearHover();
        server.SendPointerEvent(action, pt.X, pt.Y, button, delta.X, delta.Y, alt, ctrl, shift);
    }

    public bool HandleEscape()
    {
        if (IsInspectorActive)
        {
            IsInspectorActive = false;
            return true;
        }

        if (SourceView != null)
        {
            SourceView = null;
            return true;
        }

        if (SelectedElement != null)
        {
            ClearSelection();
            return true;
        }

        return false;
    }

    public void RequestInspectContextMenu(Point pt)
    {
        server.RequestContextMenuHitTest(pt.X, pt.Y);
    }

    private void RequestHoverHitTest(Point pt)
    {
        if (isHoverRequestPending && Stopwatch.GetElapsedTime(lastHoverRequestTimestamp).TotalMilliseconds < 200)
        {
            pendingHoverPoint = pt;
            return;
        }

        lastHoverRequestTimestamp = Stopwatch.GetTimestamp();
        isHoverRequestPending = true;
        server.RequestHitTest(pt.X, pt.Y, isHover: true);
    }

    public void ClearHover()
    {
        pendingHoverPoint = null;
        HoveredElement = null;
    }

    public void ClearSelection()
    {
        selectionToRestore = null;
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

    /// <summary>
    /// Shows the XAML of the selected element inside ShowMe, at the line where it is defined.
    /// </summary>
    public void ViewSelectedXaml()
    {
        if (SelectedElement == null) return;

        ViewXaml(SelectedFile(SelectedElement), SelectedElement.LineNumber, SelectedElement.LinePosition);
    }

    public void ViewXaml(string file, int line, int column)
    {
        var text = Result.Try(() => File.ReadAllText(file), e => e.Message);
        if (text.IsFailure)
        {
            Status = $"No se pudo leer {Path.GetFileName(file)}: {text.Error}";
            return;
        }

        SourceView = new XamlSourceViewModel(file, text.Value, line, column);
    }

    public void OpenSelectedInEditor()
    {
        if (SelectedElement == null) return;

        OpenInEditor(SelectedFile(SelectedElement), SelectedElement.LineNumber, SelectedElement.LinePosition);
    }

    public void OpenInEditor(string file, int line, int column)
    {
        var opened = codeEditor
            .ToResult("no hay ningún editor de código configurado (Más acciones → Editor de código…)")
            .Bind(editor => editor.Open(file, Math.Max(line, 1), Math.Max(column, 1)));

        Status = opened.IsSuccess
            ? $"Abriendo {Path.GetFileName(file)}:{line} con {opened.Value}."
            : $"No se pudo abrir {Path.GetFileName(file)}: {opened.Error}";
    }

    private string SelectedFile(ElementInspectionInfo element) =>
        element.ResolvedFilePath ?? SourceFileResolver.Resolve(element.SourceUri, Target);

    /// <summary>
    /// Opens, in place of this one, the view that defines the selected element.
    /// </summary>
    public Task OpenDefiningViewAsync()
    {
        if (SelectedElement == null || !CanOpenDefiningView || openView == null) return Task.CompletedTask;

        return openView(SelectedFile(SelectedElement));
    }

    private bool CanOpenView(string file) =>
        openView != null &&
        !string.Equals(Path.GetFullPath(file), Path.GetFullPath(Target.AxamlPath), StringComparison.Ordinal) &&
        PreviewableView.CanPreview(file);

    /// <summary>
    /// Selects again, once the preview answers, the element that was selected when this view was left. The element is
    /// looked up by its identity, so it is found wherever a new size or the scroll have moved it.
    /// </summary>
    public void RestoreSelection(ElementInspectionInfo element)
    {
        selectionToRestore = element;
        server.RequestFindElement(
            element.TypeName,
            element.ElementName,
            element.LineNumber,
            element.LinePosition,
            element.SourceUri,
            element.TreePath);
    }

    public void SelectElement(InspectMenuItemViewModel item)
    {
        IsInspectorActive = false;
        selectionToRestore = null;
        SelectedElement = new ElementInspectionInfo(
            TypeName: item.TypeName,
            ElementName: item.ElementName,
            LineNumber: item.LineNumber,
            LinePosition: item.LinePosition,
            SourceUri: item.SourceUri,
            Bounds: item.Bounds,
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>(),
            ResolvedFilePath: item.ResolvedFilePath,
            TreePath: item.TreePath
        );
        SelectionBounds = item.Bounds;
    }

    public void HoverElement(InspectMenuItemViewModel item)
    {
        HoveredElement = new ElementInspectionInfo(
            TypeName: item.TypeName,
            ElementName: item.ElementName,
            LineNumber: item.LineNumber,
            LinePosition: item.LinePosition,
            SourceUri: item.SourceUri,
            Bounds: item.Bounds,
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>(),
            ResolvedFilePath: item.ResolvedFilePath
        );
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
                var pixelFormat = frame.Format == ShowMePixelFormat.Bgra8888
                    ? global::Avalonia.Platform.PixelFormat.Bgra8888
                    : global::Avalonia.Platform.PixelFormat.Rgba8888;

                var bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    pixelFormat,
                    AlphaFormat.Premul);

                using (var locked = bitmap.Lock())
                {
                    if (locked.RowBytes == frame.Stride)
                    {
                        Marshal.Copy(frame.PixelData, 0, locked.Address, frame.PixelData.Length);
                    }
                    else
                    {
                        var minStride = Math.Min(locked.RowBytes, frame.Stride);
                        for (int y = 0; y < frame.Height; y++)
                        {
                            Marshal.Copy(frame.PixelData, y * frame.Stride, locked.Address + (y * locked.RowBytes), minStride);
                        }
                    }
                }

                CurrentBitmap = bitmap;
                if (IsCatalogActive || (!hasUserSetDimensions && !IsResizing))
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
        DispatchToUI(() =>
        {
            if (hit.IsHover)
            {
                if (hit.Found && IsInspectorActive)
                {
                    var bounds = new Rect(hit.BoundsX, hit.BoundsY, hit.BoundsWidth, hit.BoundsHeight);
                    var resolvedFile = SourceFileResolver.Resolve(hit.SourceUri, Target);
                    HoveredElement = new ElementInspectionInfo(
                        TypeName: hit.TypeName,
                        ElementName: hit.ElementName,
                        LineNumber: hit.LineNumber,
                        LinePosition: hit.LinePosition,
                        SourceUri: hit.SourceUri,
                        ResolvedFilePath: resolvedFile,
                        Bounds: bounds,
                        Classes: hit.Classes ?? [],
                        Ancestors: hit.AncestorTree ?? [],
                        Properties: hit.Properties ?? new Dictionary<string, string>()
                    );
                }
                else
                {
                    HoveredElement = null;
                }

                isHoverRequestPending = false;
                if (pendingHoverPoint.HasValue)
                {
                    var next = pendingHoverPoint.Value;
                    pendingHoverPoint = null;
                    RequestHoverHitTest(next);
                }
            }
            else
            {
                if (hit.Found)
                {
                    var bounds = new Rect(hit.BoundsX, hit.BoundsY, hit.BoundsWidth, hit.BoundsHeight);
                    var resolvedFile = SourceFileResolver.Resolve(hit.SourceUri, Target);
                    selectionToRestore = null;
                    SelectedElement = new ElementInspectionInfo(
                        TypeName: hit.TypeName,
                        ElementName: hit.ElementName,
                        LineNumber: hit.LineNumber,
                        LinePosition: hit.LinePosition,
                        SourceUri: hit.SourceUri,
                        ResolvedFilePath: resolvedFile,
                        Bounds: bounds,
                        Classes: hit.Classes ?? [],
                        Ancestors: hit.AncestorTree ?? [],
                        Properties: hit.Properties ?? new Dictionary<string, string>(),
                        TreePath: hit.TreePath
                    );
                    SelectionBounds = bounds;

                    if (viewXamlOnNextSelection)
                    {
                        viewXamlOnNextSelection = false;
                        ViewSelectedXaml();
                    }
                }
                else
                {
                    viewXamlOnNextSelection = false;
                    ClearSelection();
                }
            }
        });
    }

    private void OnContextMenuHitTestResultReceived(ContextMenuHitTestResponseMessage hit)
    {
        void Handle()
        {
            if (hit.Items == null || hit.Items.Count == 0) return;

            var menuItems = hit.Items.Select(item =>
            {
                var resolved = SourceFileResolver.Resolve(item.SourceUri, Target);
                var relPath = SourceFileResolver.GetRelativePathIfRemote(resolved, Target);
                var bounds = new Rect(item.BoundsX, item.BoundsY, item.BoundsWidth, item.BoundsHeight);
                return new InspectMenuItemViewModel(
                    typeName: item.TypeName,
                    elementName: item.ElementName,
                    lineNumber: item.LineNumber,
                    linePosition: item.LinePosition,
                    sourceUri: item.SourceUri,
                    resolvedFilePath: resolved,
                    relativeFilePath: relPath,
                    bounds: bounds,
                    onSelect: SelectElement,
                    onViewXaml: menuItem =>
                    {
                        SelectElement(menuItem);
                        ViewXaml(menuItem.ResolvedFilePath, menuItem.LineNumber, menuItem.LinePosition);
                    },
                    onOpenInEditor: CanOpenInEditor
                        ? menuItem =>
                        {
                            SelectElement(menuItem);
                            OpenInEditor(menuItem.ResolvedFilePath, menuItem.LineNumber, menuItem.LinePosition);
                        }
                        : null,
                    onOpenView: CanOpenView(resolved)
                        ? menuItem =>
                        {
                            SelectElement(menuItem);
                            return openView!(menuItem.ResolvedFilePath);
                        }
                        : null,
                    treePath: item.TreePath
                );
            }).ToList();

            RequestShowContextMenu?.Invoke(new Point(hit.X, hit.Y), menuItems);
        }

        DispatchToUI(Handle);
    }

    private void OnFindElementResultReceived(FindElementResponseMessage found)
    {
        DispatchToUI(() =>
        {
            // Nothing to restore, or the user selected something else meanwhile.
            if (selectionToRestore is not { } wanted) return;
            selectionToRestore = null;

            if (found.Item is not { } item) return;

            var bounds = new Rect(item.BoundsX, item.BoundsY, item.BoundsWidth, item.BoundsHeight);
            SelectedElement = wanted with { Bounds = bounds, TreePath = item.TreePath };
            SelectionBounds = bounds;
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
#pragma warning disable CS0618
                CurrentBitmap.Save(stream);
#pragma warning restore CS0618
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

    private void OnCatalogInfoReceived(CatalogInfoMessage msg)
    {
        DispatchToUI(() =>
        {
            CatalogTreeNodes.Clear();
            CatalogFlatItems.Clear();
            FilteredFlatItems.Clear();

            CatalogTitle = msg.Title;
            HasPreviewWith = msg.HasPreviewWith;
            CatalogItemCount = msg.AllItems.Count;
            IsCatalogActive = msg.AllItems.Count > 0;
            isSidebarVisible = true;
            this.RaisePropertyChanged(nameof(IsSidebarVisible));

            if (msg.RootGroup != null)
            {
                CatalogTreeNodes.Add(new CatalogGroupNodeViewModel(msg.RootGroup));
            }

            foreach (var item in msg.AllItems)
            {
                var vm = new CatalogItemViewModel(item);
                CatalogFlatItems.Add(vm);
                FilteredFlatItems.Add(vm);
            }

            if (HasPreviewWith)
            {
                sidebarMode = "PreviewWith";
            }
            else
            {
                sidebarMode = "Tree";
            }
            this.RaisePropertyChanged(nameof(SidebarMode));
            this.RaisePropertyChanged(nameof(IsTreeMode));
            this.RaisePropertyChanged(nameof(IsFlatMode));
            this.RaisePropertyChanged(nameof(IsPreviewWithMode));
        });
    }

    private void ApplyCatalogFilter(string query)
    {
        foreach (var node in CatalogTreeNodes)
        {
            node.ApplyFilter(query);
        }

        FilteredFlatItems.Clear();
        foreach (var item in CatalogFlatItems)
        {
            if (item.Matches(query))
            {
                FilteredFlatItems.Add(item);
            }
        }

        if (selectedCatalogNode == null)
        {
            server.SelectCatalogItem(null, "All", query);
        }
    }

    private void OnCatalogSelectionChanged(object? selected)
    {
        if (selected is CatalogItemViewModel item)
        {
            server.SelectCatalogItem(item.Id, "Single");
        }
        else if (selected is CatalogGroupNodeViewModel group)
        {
            server.SelectCatalogItem(group.Title, "Group");
        }
    }

    public void Dispose()
    {
        server.CatalogInfoReceived -= OnCatalogInfoReceived;
        codeEditorSubscription.Dispose();
        dragDebounceCts?.Cancel();
        dragDebounceCts?.Dispose();
        fileWatcher?.Dispose();
        server.Dispose();
    }
}
