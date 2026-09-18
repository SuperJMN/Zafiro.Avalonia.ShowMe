using Avalonia;
using Zafiro.Avalonia.Mcp.AppHost;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe;

internal sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var cli = CommandLineArgs.Parse(args);
        if (cli.ShowHelp)
        {
            CommandLineArgs.PrintHelp();
            return 0;
        }

        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseMcpDiagnostics()
            .WithInterFont()
            .LogToTrace();
}
