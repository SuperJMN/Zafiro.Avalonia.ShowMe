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

    [Fact]
    public void File_Uri_Is_Normalized_To_Local_Path()
    {
        var result = CommandLineArgs.Parse(["file:///home/user/Views/MyView.axaml"]);
        Assert.Equal("/home/user/Views/MyView.axaml", result.FilePath);
    }

    [Fact]
    public void File_Uri_With_Url_Encoding_Is_Decoded()
    {
        var result = CommandLineArgs.Parse(["file:///home/user/My%20Folder/My%20View.axaml"]);
        Assert.Equal("/home/user/My Folder/My View.axaml", result.FilePath);
    }

    [Fact]
    public void Project_Option_With_File_Uri_Is_Normalized()
    {
        var result = CommandLineArgs.Parse(["--project", "file:///home/user/Project/App.csproj"]);
        Assert.Equal("/home/user/Project/App.csproj", result.ProjectPath);
    }

    [Fact]
    public void Quoted_Path_Is_Trimmed()
    {
        var result = CommandLineArgs.Parse(["\"file:///home/user/Views/MyView.axaml\""]);
        Assert.Equal("/home/user/Views/MyView.axaml", result.FilePath);
    }
}
