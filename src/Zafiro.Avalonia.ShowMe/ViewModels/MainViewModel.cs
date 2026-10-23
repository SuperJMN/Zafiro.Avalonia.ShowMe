using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.UI.Commands;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(60);

    private readonly IStorageService storageService;
    private readonly CodeEditor? codeEditor;
    private PreviewSessionViewModel? currentSession;
    private EditorSettingsViewModel? editorSettings;
    private ThemeOption selectedTheme;
    private bool isLoading;
    private string loadingMessage = "Cargando...";
    private string? loadingDetail;
    private LoadProgress? currentStep;
    private string? errorMessage;
    private bool hasError;
    private readonly List<string> recentFiles = [];
    private readonly Stack<ViewVisit> backHistory = new();
    private ViewVisit? currentVisit;
    private readonly BehaviorSubject<bool> canGoBack = new(false);
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private int loadGeneration;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(XamlThemeModifier.ThemeDefault, "Aplicación"),
        new(XamlThemeModifier.ThemeLight, "Claro"),
        new(XamlThemeModifier.ThemeDark, "Oscuro")
    ];

    public ThemeOption SelectedTheme
    {
        get => selectedTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref selectedTheme, value);
            ApplyTheme(value.Id);
        }
    }

    public PreviewSessionViewModel? CurrentSession
    {
        get => currentSession;
        private set
        {
            this.RaiseAndSetIfChanged(ref currentSession, value);
            this.RaisePropertyChanged(nameof(IsWelcomeVisible));
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref isLoading, value);
            this.RaisePropertyChanged(nameof(IsWelcomeVisible));
        }
    }

    public bool IsWelcomeVisible => CurrentSession == null && !IsLoading;

    public string LoadingMessage
    {
        get => loadingMessage;
        private set => this.RaiseAndSetIfChanged(ref loadingMessage, value);
    }

    public string? LoadingDetail
    {
        get => loadingDetail;
        private set => this.RaiseAndSetIfChanged(ref loadingDetail, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => this.RaiseAndSetIfChanged(ref errorMessage, value);
    }

    public bool HasError
    {
        get => hasError;
        private set => this.RaiseAndSetIfChanged(ref hasError, value);
    }

    public IReadOnlyList<string> RecentFiles => recentFiles;

    /// <summary>
    /// True when there is a previous view to go back to with "Atrás".
    /// </summary>
    public bool CanGoBack => canGoBack.Value;

    public string BackToolTip => backHistory.TryPeek(out var previous)
        ? $"Volver a {Path.GetFileName(previous.AxamlPath)} (Alt+Izquierda)"
        : "Volver a la vista anterior (Alt+Izquierda)";

    public IEnhancedCommand GoBackCommand { get; }

    /// <summary>
    /// The open "Editor de código…" dialog, or null when it is closed.
    /// </summary>
    public EditorSettingsViewModel? EditorSettings
    {
        get => editorSettings;
        private set => this.RaiseAndSetIfChanged(ref editorSettings, value);
    }

    public IEnhancedCommand ConfigureEditorCommand { get; }

    public IEnhancedCommand OpenFileCommand { get; }
    public IEnhancedCommand DismissErrorCommand { get; }

    public MainViewModel(IStorageService storageService, string initialTheme = XamlThemeModifier.ThemeDefault, CodeEditor? codeEditor = null)
    {
        this.storageService = storageService;
        this.codeEditor = codeEditor;
        selectedTheme = ThemeOptions.FirstOrDefault(t => string.Equals(t.Id, initialTheme, StringComparison.OrdinalIgnoreCase))
                        ?? ThemeOptions[0];

        OpenFileCommand = EnhancedCommand.Create(OpenFileAsync);
        DismissErrorCommand = EnhancedCommand.Create(() => { HasError = false; });
        GoBackCommand = EnhancedCommand.Create(GoBackAsync, canGoBack);
        ConfigureEditorCommand = EnhancedCommand.Create(
            () => EditorSettings = new EditorSettingsViewModel(codeEditor!, () => EditorSettings = null),
            Observable.Return(codeEditor != null));
    }

    public async Task OpenFileAsync()
    {
        var path = await storageService.OpenAxamlFileDialogAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadFileAsync(path);
        }
    }

    /// <summary>
    /// Opens another view in this window, with its own project context, remembering the current one for "Atrás".
    /// </summary>
    public Task OpenViewAsync(string axamlPath) => LoadFileAsync(axamlPath);

    /// <summary>
    /// Goes back to the previous view with the size the user chose for it and, if it is still there, its selection.
    /// </summary>
    public Task GoBackAsync()
    {
        if (!backHistory.TryPop(out var previous)) return Task.CompletedTask;

        UpdateBackHistory();
        return LoadAsync(previous, rememberCurrent: false);
    }

    public Task LoadFileAsync(
        string axamlPath,
        string? explicitProjectPath = null,
        int? explicitWidth = null,
        int? explicitHeight = null)
    {
        return LoadAsync(new ViewVisit(axamlPath, explicitProjectPath, explicitWidth, explicitHeight), rememberCurrent: true);
    }

    /// <summary>
    /// Shows <paramref name="visit"/> in place of the current view. Loads run one at a time and the last one requested
    /// wins: a load that a newer one supersedes stops at its next step and never starts, or disposes, its previewer.
    /// </summary>
    private async Task LoadAsync(ViewVisit visit, bool rememberCurrent)
    {
        var axamlPath = visit.AxamlPath;
        var generation = 0;
        IProgress<LoadProgress>? progress = null;
        PreviewSessionViewModel? session = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            generation = Interlocked.Increment(ref loadGeneration);
            HasError = false;
            ErrorMessage = null;
            currentStep = null;
            LoadingMessage = $"Abriendo '{Path.GetFileName(axamlPath)}'...";
            LoadingDetail = null;
            IsLoading = true;
            progress = new Progress<LoadProgress>(step =>
            {
                if (IsLatest(generation)) ShowProgress(step);
            });

            if (rememberCurrent && CurrentSession is { } current && currentVisit is { } shown && !IsSameFile(current.Target.AxamlPath, axamlPath))
            {
                backHistory.Push(shown with { ChosenSize = current.ChosenSize, Selection = current.SelectedElement });
                UpdateBackHistory();
            }

            CurrentSession?.Dispose();
            CurrentSession = null;
            currentVisit = null;
        });

        await loadLock.WaitAsync();
        try
        {
            if (!IsLatest(generation)) return;

            var resolveResult = await PreviewTargetResolver.ResolveAsync(axamlPath, visit.ProjectPath, progress);
            if (!IsLatest(generation)) return;

            if (resolveResult.IsFailure)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ShowFailure(resolveResult.Error));
                return;
            }

            var target = resolveResult.Value;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (!IsLatest(generation)) return;

                ShowProgress(new LoadProgress(LoadStep.StartingPreviewer, "Iniciando el previewer", target.TargetName));

                var initialXaml = await File.ReadAllTextAsync(target.AxamlPath);
                var server = new PreviewServer(target);
                var firstRender = FirstRenderOf(server);
                session = new PreviewSessionViewModel(
                    target,
                    server,
                    storageService,
                    SelectedTheme.Id,
                    visit.Width,
                    visit.Height,
                    codeEditor,
                    OpenViewAsync,
                    visit.ChosenSize);

                await server.StartAsync(initialXaml, SelectedTheme.Id);

                ShowProgress(new LoadProgress(LoadStep.Rendering, "Renderizando", Path.GetFileName(target.AxamlPath)));
                if (await Task.WhenAny(firstRender, Task.Delay(RenderTimeout)) != firstRender)
                {
                    throw new TimeoutException($"El previewer no respondió en {RenderTimeout.TotalSeconds:0} s.");
                }

                await firstRender;

                if (!IsLatest(generation))
                {
                    session.Dispose();
                    return;
                }

                if (!recentFiles.Contains(axamlPath))
                {
                    recentFiles.Insert(0, axamlPath);
                    if (recentFiles.Count > 10) recentFiles.RemoveAt(recentFiles.Count - 1);
                    this.RaisePropertyChanged(nameof(RecentFiles));
                }

                CurrentSession = session;
                currentVisit = visit with { ChosenSize = null, Selection = null };
                if (visit.Selection is { } selection)
                {
                    session.RestoreSelection(selection);
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                session?.Dispose();
                if (IsLatest(generation)) ShowFailure(ex.Message);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsLatest(generation)) IsLoading = false;
            });
            loadLock.Release();
        }
    }

    private bool IsLatest(int generation) => generation == Volatile.Read(ref loadGeneration);

    private void UpdateBackHistory()
    {
        canGoBack.OnNext(backHistory.Count > 0);
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(BackToolTip));
    }

    private static bool IsSameFile(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);

    private void ShowProgress(LoadProgress step)
    {
        currentStep = step;
        LoadingMessage = $"{step.Title}...";
        LoadingDetail = step.Detail;
    }

    private void ShowFailure(string error)
    {
        ErrorMessage = currentStep == null ? error : $"Falló el paso «{currentStep.Title}»: {error}";
        HasError = true;
    }

    private static Task FirstRenderOf(PreviewServer server)
    {
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.XamlStatusReceived += _ => rendered.TrySetResult();
        server.ErrorOccurred += error => rendered.TrySetException(new InvalidOperationException(error));
        return rendered.Task;
    }

    private void ApplyTheme(string themeId)
    {
        // 1. Cambiar el tema de la aplicación ShowMe
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = themeId switch
            {
                XamlThemeModifier.ThemeDark => ThemeVariant.Dark,
                XamlThemeModifier.ThemeLight => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
        }

        // 2. Notificar a la sesión activa para actualizar el XAML previewed
        CurrentSession?.UpdateTheme(themeId);
    }
}

/// <summary>
/// A view shown in the window, with what is needed to show it again as it was: how it was opened (its project and the
/// size given on the command line) and, once left behind by navigation, the size the user chose and the selection.
/// </summary>
public sealed record ViewVisit(
    string AxamlPath,
    string? ProjectPath = null,
    int? Width = null,
    int? Height = null,
    PixelSize? ChosenSize = null,
    ElementInspectionInfo? Selection = null);
