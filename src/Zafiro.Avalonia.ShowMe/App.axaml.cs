using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.Avalonia.ShowMe.ViewModels;
using Zafiro.Avalonia.ShowMe.Views;

namespace Zafiro.Avalonia.ShowMe;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var cliArgs = CommandLineArgs.Parse(desktop.Args ?? []);

            var storageService = new AvaloniaStorageService();
            var mainViewModel = new MainViewModel(storageService, cliArgs.Theme ?? XamlThemeModifier.ThemeDefault);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) =>
            {
                mainViewModel.CurrentSession?.Dispose();
            };

            if (!string.IsNullOrWhiteSpace(cliArgs.FilePath))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await mainViewModel.LoadFileAsync(
                        cliArgs.FilePath,
                        cliArgs.ProjectPath,
                        cliArgs.Width,
                        cliArgs.Height);
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
