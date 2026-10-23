using System.Reactive;
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
    private readonly string? editorTemplate;
    private PreviewSessionViewModel? currentSession;
    private ThemeOption selectedTheme;
    private bool isLoading;
    private string loadingMessage = "Cargando...";
    private string? loadingDetail;
    private LoadProgress? currentStep;
    private string? errorMessage;
    private bool hasError;
    private readonly List<string> recentFiles = [];

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

    public IEnhancedCommand OpenFileCommand { get; }
    public IEnhancedCommand DismissErrorCommand { get; }

    public MainViewModel(IStorageService storageService, string initialTheme = XamlThemeModifier.ThemeDefault, string? editorTemplate = null)
    {
        this.storageService = storageService;
        this.editorTemplate = editorTemplate;
        selectedTheme = ThemeOptions.FirstOrDefault(t => string.Equals(t.Id, initialTheme, StringComparison.OrdinalIgnoreCase))
                        ?? ThemeOptions[0];

        OpenFileCommand = EnhancedCommand.Create(OpenFileAsync);
        DismissErrorCommand = EnhancedCommand.Create(() => { HasError = false; });
    }

    public async Task OpenFileAsync()
    {
        var path = await storageService.OpenAxamlFileDialogAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadFileAsync(path);
        }
    }

    public async Task LoadFileAsync(
        string axamlPath,
        string? explicitProjectPath = null,
        int? explicitWidth = null,
        int? explicitHeight = null)
    {
        IProgress<LoadProgress>? progress = null;
        PreviewSessionViewModel? session = null;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HasError = false;
                ErrorMessage = null;
                currentStep = null;
                LoadingMessage = $"Abriendo '{Path.GetFileName(axamlPath)}'...";
                LoadingDetail = null;
                IsLoading = true;
                progress = new Progress<LoadProgress>(ShowProgress);

                CurrentSession?.Dispose();
                CurrentSession = null;
            });

            var resolveResult = await PreviewTargetResolver.ResolveAsync(axamlPath, explicitProjectPath, progress);
            if (resolveResult.IsFailure)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ShowFailure(resolveResult.Error));
                return;
            }

            var target = resolveResult.Value;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ShowProgress(new LoadProgress(LoadStep.StartingPreviewer, "Iniciando el previewer", target.TargetName));

                var initialXaml = await File.ReadAllTextAsync(target.AxamlPath);
                var server = new PreviewServer(target);
                var firstRender = FirstRenderOf(server);
                session = new PreviewSessionViewModel(
                    target,
                    server,
                    storageService,
                    SelectedTheme.Id,
                    explicitWidth,
                    explicitHeight,
                    editorTemplate);

                await server.StartAsync(initialXaml, SelectedTheme.Id);

                ShowProgress(new LoadProgress(LoadStep.Rendering, "Renderizando", Path.GetFileName(target.AxamlPath)));
                if (await Task.WhenAny(firstRender, Task.Delay(RenderTimeout)) != firstRender)
                {
                    throw new TimeoutException($"El previewer no respondió en {RenderTimeout.TotalSeconds:0} s.");
                }

                await firstRender;

                if (!recentFiles.Contains(axamlPath))
                {
                    recentFiles.Insert(0, axamlPath);
                    if (recentFiles.Count > 10) recentFiles.RemoveAt(recentFiles.Count - 1);
                    this.RaisePropertyChanged(nameof(RecentFiles));
                }

                CurrentSession = session;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                session?.Dispose();
                ShowFailure(ex.Message);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

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
