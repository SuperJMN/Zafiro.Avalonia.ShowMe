using Xunit;
using Xunit.Abstractions;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Protocol;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class PreviewServerIntegrationTests
{
    private readonly ITestOutputHelper output;

    public PreviewServerIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

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

        server.StatusChanged += s => output.WriteLine($"[Status] {s}");
        server.ErrorOccurred += e => output.WriteLine($"[Error] {e}");
        server.LogReceived += l => output.WriteLine(l);
        server.XamlStatusReceived += x => output.WriteLine($"[XamlStatus] Success={x.Success}, Error={x.Error}");

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame =>
        {
            output.WriteLine($"[FrameReceived] {frame.Width}x{frame.Height}, {frame.PixelData?.Length} bytes");
            frameTcs.TrySetResult(frame);
        };

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, completed);

        var frame = await frameTcs.Task;
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
        Assert.NotNull(frame.PixelData);
        Assert.True(frame.PixelData.Length > 0);
    }

    [Fact]
    public async Task PreviewServer_HitTest_Inspects_Element()
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

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        await frameTcs.Task;

        var hitTcs = new TaskCompletionSource<HitTestResponseMessage>();
        server.HitTestResultReceived += res => hitTcs.TrySetResult(res);

        server.RequestHitTest(50, 70);

        var hitCompleted = await Task.WhenAny(hitTcs.Task, Task.Delay(5000));
        Assert.Same(hitTcs.Task, hitCompleted);

        var hit = await hitTcs.Task;
        Assert.NotNull(hit);
        Assert.True(hit.Found);
        Assert.NotEmpty(hit.TypeName);
        output.WriteLine($"[HitTest] Type={hit.TypeName}, Element={hit.ElementName}, Line={hit.LineNumber}:{hit.LinePosition}, Bounds=({hit.BoundsX},{hit.BoundsY},{hit.BoundsWidth}x{hit.BoundsHeight})");
    }

    [Fact]
    public async Task PreviewServer_Loads_Proteus_SettingsView()
    {
        var proteusAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Pages/Settings/SettingsView.axaml";
        if (!File.Exists(proteusAxaml))
        {
            return;
        }

        var resolveResult = await PreviewTargetResolver.ResolveAsync(proteusAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 960, 1048);

        server.StatusChanged += s => output.WriteLine($"[Status] {s}");
        server.ErrorOccurred += e => output.WriteLine($"[Error] {e}");
        server.LogReceived += l => output.WriteLine(l);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame =>
        {
            output.WriteLine($"[FrameReceived] {frame.Width}x{frame.Height}, {frame.PixelData?.Length} bytes");
            frameTcs.TrySetResult(frame);
        };

        var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
        server.XamlStatusReceived += x =>
        {
            output.WriteLine($"[XamlStatus] Success={x.Success}, Error={x.Error}");
            xamlStatusTcs.TrySetResult(x);
        };

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
        Assert.Same(xamlStatusTcs.Task, completed);

        var status = await xamlStatusTcs.Task;
        Assert.True(status.Success, status.Error);

        var frameCompleted = await Task.WhenAny(frameTcs.Task, Task.Delay(5000));
        Assert.Same(frameTcs.Task, frameCompleted);
        var frame = await frameTcs.Task;
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
        Assert.Equal(ShowMePixelFormat.Rgba8888, frame.Format);

        var hitTcs = new TaskCompletionSource<HitTestResponseMessage>();
        server.HitTestResultReceived += res => hitTcs.TrySetResult(res);

        server.RequestHitTest(480, 20);
        var hitCompleted = await Task.WhenAny(hitTcs.Task, Task.Delay(5000));
        Assert.Same(hitTcs.Task, hitCompleted);
        var hit = await hitTcs.Task;
        Assert.NotNull(hit);
        output.WriteLine($"[HitTest] Found={hit.Found}, Type={hit.TypeName}, Element={hit.ElementName}, Line={hit.LineNumber}");
        Assert.True(hit.Properties.ContainsKey("FontSize"));
        Assert.Equal("48", hit.Properties["FontSize"]);
    }
}
