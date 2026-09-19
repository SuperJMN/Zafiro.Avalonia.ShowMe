using System;
using System.IO;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class SourceFileResolverTests : IDisposable
{
    private readonly string tempDir;

    public SourceFileResolverTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "showme_source_resolver_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch { }
    }

    private PreviewTarget CreateDummyTarget(string axamlPath, string projectPath)
    {
        return new PreviewTarget(
            AxamlPath: axamlPath,
            ContainingProjectPath: projectPath,
            HostProjectPath: projectPath,
            TargetAssemblyPath: Path.Combine(tempDir, "bin", "Dummy.dll"),
            XamlAssemblyPath: Path.Combine(tempDir, "bin", "Dummy.dll"),
            TargetDirectory: Path.GetDirectoryName(projectPath)!,
            TargetName: "Dummy",
            DesignerHostPath: Path.Combine(tempDir, "designer.dll"),
            RuntimeConfigPath: Path.Combine(tempDir, "dummy.runtimeconfig.json"),
            DepsJsonPath: Path.Combine(tempDir, "dummy.deps.json"),
            RelativeXamlPath: "/Test.axaml",
            InitialWidth: 800,
            InitialHeight: 600
        );
    }

    [Fact]
    public void Null_Or_Empty_Returns_Target_AxamlPath()
    {
        var target = CreateDummyTarget("/path/to/MainView.axaml", "/path/to/Project.csproj");

        Assert.Equal("/path/to/MainView.axaml", SourceFileResolver.Resolve(null, target));
        Assert.Equal("/path/to/MainView.axaml", SourceFileResolver.Resolve("", target));
        Assert.Equal("/path/to/MainView.axaml", SourceFileResolver.Resolve("   ", target));
    }

    [Fact]
    public void File_Uri_Returns_Local_Path_When_Exists()
    {
        var filePath = Path.Combine(tempDir, "ExistingView.axaml");
        File.WriteAllText(filePath, "<UserControl />");

        var target = CreateDummyTarget(Path.Combine(tempDir, "MainView.axaml"), Path.Combine(tempDir, "Project.csproj"));
        var fileUri = new Uri(filePath).AbsoluteUri;

        var resolved = SourceFileResolver.Resolve(fileUri, target);
        Assert.Equal(filePath, resolved);
    }

    [Fact]
    public void Avares_Uri_Resolves_In_Containing_Project_Directory()
    {
        var projectDir = Path.Combine(tempDir, "MyApp");
        var viewsDir = Path.Combine(projectDir, "Views");
        Directory.CreateDirectory(viewsDir);

        var mainView = Path.Combine(viewsDir, "MainView.axaml");
        var cardView = Path.Combine(viewsDir, "CardView.axaml");
        var csproj = Path.Combine(projectDir, "MyApp.csproj");

        File.WriteAllText(mainView, "<UserControl />");
        File.WriteAllText(cardView, "<UserControl />");
        File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var target = CreateDummyTarget(mainView, csproj);

        var resolved = SourceFileResolver.Resolve("avares://MyApp/Views/CardView.axaml", target);

        Assert.Equal(cardView, resolved);
    }

    [Fact]
    public void Avares_Uri_Resolves_Sibling_Project_In_Solution_Root()
    {
        var solutionDir = Path.Combine(tempDir, "MySolution");
        var appDir = Path.Combine(solutionDir, "MyApp");
        var controlsDir = Path.Combine(solutionDir, "MyApp.Controls", "Cards");

        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(controlsDir);

        File.WriteAllText(Path.Combine(solutionDir, "MySolution.sln"), "");
        var mainView = Path.Combine(appDir, "MainView.axaml");
        var appCsproj = Path.Combine(appDir, "MyApp.csproj");
        var nestedControl = Path.Combine(controlsDir, "StatusCard.axaml");
        var controlsCsproj = Path.Combine(solutionDir, "MyApp.Controls", "MyApp.Controls.csproj");

        File.WriteAllText(mainView, "<UserControl />");
        File.WriteAllText(appCsproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(nestedControl, "<UserControl />");
        File.WriteAllText(controlsCsproj, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var target = CreateDummyTarget(mainView, appCsproj);

        var resolved = SourceFileResolver.Resolve("avares://MyApp.Controls/Cards/StatusCard.axaml", target);

        Assert.Equal(nestedControl, resolved);
    }

    [Fact]
    public void Non_Existent_Avares_Falls_Back_To_AxamlPath()
    {
        var target = CreateDummyTarget("/path/to/MainView.axaml", "/path/to/Project.csproj");

        var resolved = SourceFileResolver.Resolve("avares://NonExistentApp/Views/MissingView.axaml", target);

        Assert.Equal("/path/to/MainView.axaml", resolved);
    }
}
