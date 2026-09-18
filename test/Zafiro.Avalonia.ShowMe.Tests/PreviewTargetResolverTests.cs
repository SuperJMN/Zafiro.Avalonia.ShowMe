using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class PreviewTargetResolverTests
{
    [Fact]
    public async Task NonExistent_File_Fails()
    {
        var result = await PreviewTargetResolver.ResolveAsync("/path/to/nonexistent/file.axaml");
        Assert.True(result.IsFailure);
        Assert.Contains("no existe", result.Error);
    }

    [Fact]
    public async Task Resolves_SampleApp_MainWindow()
    {
        var sampleAxaml = "/mnt/fast/Repos/AvaloniaMcp/samples/SampleApp/MainWindow.axaml";
        if (!File.Exists(sampleAxaml))
        {
            return; // Skip if environment does not have sample
        }

        var result = await PreviewTargetResolver.ResolveAsync(sampleAxaml);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        var target = result.Value;

        Assert.Equal("SampleApp", target.TargetName);
        Assert.True(File.Exists(target.TargetAssemblyPath));
        Assert.True(File.Exists(target.DesignerHostPath));
        Assert.Equal("/MainWindow.axaml", target.RelativeXamlPath);
        Assert.Equal(900, target.InitialWidth);
        Assert.Equal(650, target.InitialHeight);
    }

    [Fact]
    public async Task Resolves_Proteus_DevicesView()
    {
        var proteusAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Pages/Service/DevicesView.axaml";
        if (!File.Exists(proteusAxaml))
        {
            return;
        }

        var result = await PreviewTargetResolver.ResolveAsync(proteusAxaml);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        var target = result.Value;

        Assert.Equal("Proteus.Ui", target.TargetName);
        Assert.True(File.Exists(target.TargetAssemblyPath));
        Assert.True(File.Exists(target.XamlAssemblyPath));
        Assert.True(File.Exists(target.DesignerHostPath));
        Assert.Equal("/Service/DevicesView.axaml", target.RelativeXamlPath);
        Assert.Equal(600, target.InitialWidth);
        Assert.Equal(960, target.InitialHeight);
    }
}
