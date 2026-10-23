using Avalonia;
using Avalonia.Headless;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Zafiro.Avalonia.ShowMe.Host.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}
