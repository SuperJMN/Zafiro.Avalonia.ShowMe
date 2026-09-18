using System.Reactive;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Services;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private readonly IStorageService storageService;
    private PreviewSessionViewModel? currentSession;
    private ThemeOption selectedTheme;
    private bool isLoading;
    private string loadingMessage = "Cargando...";
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
        private set => this.RaiseAndSetIfChanged(ref currentSession, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => this.RaiseAndSetIfChanged(ref isLoading, value);
    }

    public string LoadingMessage
    {
        get => loadingMessage;
        private set => this.RaiseAndSetIfChanged(ref loadingMessage, value);
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

    public System.Windows.Input.ICommand OpenFileCommand { get; }
    public System.Windows.Input.ICommand DismissErrorCommand { get; }

    public MainViewModel(IStorageService storageService, string initialTheme = XamlThemeModifier.ThemeDefault)
    {
        this.storageService = storageService;
        selectedTheme = ThemeOptions.FirstOrDefault(t => string.Equals(t.Id, initialTheme, StringComparison.OrdinalIgnoreCase))
                        ?? ThemeOptions[0];

        OpenFileCommand = new AsyncDelegateCommand(OpenFileAsync);
        DismissErrorCommand = new DelegateCommand(() => { HasError = false; });
    }

    public async Task OpenFileAsync()
    {
        var path = await storageService.OpenAxamlFileDialogAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadFileAsync(path);
        }
    }

    public async Task LoadFileAsync(string axamlPath, string? explicitProjectPath = null)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HasError = false;
                ErrorMessage = null;
                IsLoading = true;
                LoadingMessage = $"Resolviendo contexto para '{Path.GetFileName(axamlPath)}'...";

                CurrentSession?.Dispose();
                CurrentSession = null;
            });

            var resolveResult = await PreviewTargetResolver.ResolveAsync(axamlPath, explicitProjectPath);
            if (resolveResult.IsFailure)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ErrorMessage = resolveResult.Error;
                    HasError = true;
                    IsLoading = false;
                });
                return;
            }

            var target = resolveResult.Value;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                LoadingMessage = $"Conectando con el previewer oficial ({target.TargetName})...";

                var initialXaml = await File.ReadAllTextAsync(target.AxamlPath);
                var server = new PreviewServer(target);
                var session = new PreviewSessionViewModel(target, server, storageService, SelectedTheme.Id);

                CurrentSession = session;

                await server.StartAsync(initialXaml, SelectedTheme.Id);

                if (!recentFiles.Contains(axamlPath))
                {
                    recentFiles.Insert(0, axamlPath);
                    if (recentFiles.Count > 10) recentFiles.RemoveAt(recentFiles.Count - 1);
                    this.RaisePropertyChanged(nameof(RecentFiles));
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = $"Error inesperado al cargar la previsualización: {ex.Message}";
                HasError = true;
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
