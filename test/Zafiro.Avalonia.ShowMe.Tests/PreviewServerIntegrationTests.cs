using Avalonia.Remote.Protocol.Viewport;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class PreviewServerIntegrationTests
{
    [Fact]
    public async Task PreviewServer_Connects_And_Receives_Frames()
    {
        var sampleAxaml = "/mnt/fast/Repos/AvaloniaMcp/samples/SampleApp/MainWindow.axaml";
        if (!File.Exists(sampleAxaml))
        {
            return;
        }

        var resolveResult = await PreviewTargetResolver.ResolveAsync(sampleAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 900, 650);

        var frameTcs = new TaskCompletionSource<FrameMessage>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, completed);

        var frame = await frameTcs.Task;
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
        Assert.NotNull(frame.Data);
        Assert.True(frame.Data.Length > 0);
    }
}
