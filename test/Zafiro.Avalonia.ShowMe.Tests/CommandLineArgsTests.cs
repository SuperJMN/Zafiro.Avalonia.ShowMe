using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class CommandLineArgsTests
{
    [Fact]
    public void Empty_Args_Returns_Defaults()
    {
        var result = CommandLineArgs.Parse([]);
        Assert.Null(result.FilePath);
        Assert.Null(result.ProjectPath);
        Assert.Null(result.Theme);
        Assert.Null(result.Width);
        Assert.Null(result.Height);
        Assert.False(result.ShowHelp);
    }

    [Fact]
    public void Help_Flag_Sets_ShowHelp()
    {
        var result1 = CommandLineArgs.Parse(["--help"]);
        Assert.True(result1.ShowHelp);

        var result2 = CommandLineArgs.Parse(["-h"]);
        Assert.True(result2.ShowHelp);
    }

    [Fact]
    public void Positional_FilePath_Is_Parsed()
    {
        var result = CommandLineArgs.Parse(["src/Views/MyView.axaml"]);
        Assert.Equal("src/Views/MyView.axaml", result.FilePath);
    }

    [Fact]
    public void All_Options_Are_Parsed()
    {
        var args = new[]
        {
            "src/Views/MyView.axaml",
            "--theme", "Dark",
            "-w", "1280",
            "--height", "720",
            "-p", "src/MyApp/MyApp.csproj"
        };

        var result = CommandLineArgs.Parse(args);

        Assert.Equal("src/Views/MyView.axaml", result.FilePath);
        Assert.Equal("Dark", result.Theme);
        Assert.Equal(1280, result.Width);
        Assert.Equal(720, result.Height);
        Assert.Equal("src/MyApp/MyApp.csproj", result.ProjectPath);
        Assert.False(result.ShowHelp);
    }
}
