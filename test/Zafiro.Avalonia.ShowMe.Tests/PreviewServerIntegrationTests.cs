using SkiaSharp;
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

        var frameCompleted = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, frameCompleted);
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
    public async Task PreviewServer_HitTest_Hover_Inspects_Element()
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

        var frameCompleted = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, frameCompleted);
        await frameTcs.Task;

        var hitTcs = new TaskCompletionSource<HitTestResponseMessage>();
        server.HitTestResultReceived += res => hitTcs.TrySetResult(res);

        server.RequestHitTest(50, 70, isHover: true);

        var hitCompleted = await Task.WhenAny(hitTcs.Task, Task.Delay(5000));
        Assert.Same(hitTcs.Task, hitCompleted);

        var hit = await hitTcs.Task;
        Assert.NotNull(hit);
        Assert.True(hit.Found);
        Assert.True(hit.IsHover);
        Assert.NotEmpty(hit.TypeName);
        Assert.True(hit.BoundsWidth > 0);
        Assert.True(hit.BoundsHeight > 0);
        Assert.Null(hit.Properties);
        output.WriteLine($"[HitTest Hover] Type={hit.TypeName}, Element={hit.ElementName}, Bounds=({hit.BoundsX},{hit.BoundsY},{hit.BoundsWidth}x{hit.BoundsHeight})");
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
        Assert.NotNull(hit.Properties);
        Assert.True(hit.Properties.ContainsKey("FontSize"));
        Assert.Equal("48", hit.Properties["FontSize"]);
    }

    [Fact]
    public async Task PreviewServer_Loads_CompletedRunDetailsView_And_HitTests_Nested_Control()
    {
        var runDetailsAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Pages/Production/CompletedRunDetails/CompletedRunDetailsView.axaml";
        if (!File.Exists(runDetailsAxaml))
        {
            return;
        }

        var resolveResult = await PreviewTargetResolver.ResolveAsync(runDetailsAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 1024, 1240);

        server.StatusChanged += s => output.WriteLine($"[Status] {s}");
        server.ErrorOccurred += e => output.WriteLine($"[Error] {e}");
        server.LogReceived += l => output.WriteLine(l);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
        server.XamlStatusReceived += x => xamlStatusTcs.TrySetResult(x);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
        Assert.Same(xamlStatusTcs.Task, completed);
        var status = await xamlStatusTcs.Task;
        Assert.True(status.Success, status.Error);

        var frameCompleted = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, frameCompleted);
        await frameTcs.Task;

        // Hit test at bottom where RunWarningContentView is located
        var hitTcs = new TaskCompletionSource<HitTestResponseMessage>();
        server.HitTestResultReceived += res => hitTcs.TrySetResult(res);

        server.RequestHitTest(500, 300);
        var hitCompleted = await Task.WhenAny(hitTcs.Task, Task.Delay(5000));
        Assert.Same(hitTcs.Task, hitCompleted);
        var hit = await hitTcs.Task;
        Assert.NotNull(hit);
        output.WriteLine($"[HitTest RunDetails] Found={hit.Found}, Type={hit.TypeName}, Element={hit.ElementName}, Line={hit.LineNumber}:{hit.LinePosition}, SourceUri={hit.SourceUri}");
    }

    [Fact]
    public async Task PreviewServer_ContextMenuHitTest_Returns_Hierarchy()
    {
        var runDetailsAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Pages/Production/CompletedRunDetails/CompletedRunDetailsView.axaml";
        if (!File.Exists(runDetailsAxaml))
        {
            return;
        }

        var resolveResult = await PreviewTargetResolver.ResolveAsync(runDetailsAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 1024, 1240);

        var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
        server.XamlStatusReceived += x => xamlStatusTcs.TrySetResult(x);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
        Assert.Same(xamlStatusTcs.Task, completed);
        var status = await xamlStatusTcs.Task;
        Assert.True(status.Success, status.Error);

        var frameCompleted = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, frameCompleted);
        await frameTcs.Task;

        var ctxTcs = new TaskCompletionSource<ContextMenuHitTestResponseMessage>();
        server.ContextMenuHitTestResultReceived += res => ctxTcs.TrySetResult(res);

        server.RequestContextMenuHitTest(50, 50);
        var ctxCompleted = await Task.WhenAny(ctxTcs.Task, Task.Delay(5000));
        Assert.Same(ctxTcs.Task, ctxCompleted);

        var ctx = await ctxTcs.Task;
        Assert.NotNull(ctx);
        Assert.NotEmpty(ctx.Items);

        foreach (var item in ctx.Items)
        {
            output.WriteLine($"[ContextItem] Type={item.TypeName}, Name={item.ElementName}, Line={item.LineNumber}:{item.LinePosition}, SourceUri={item.SourceUri}");
        }

        Assert.True(ctx.Items[0].LineNumber > 0);
    }

    private static void SaveFrameAsPng(ShowMeFramePacket frame, string outputPath)
    {
        var colorType = frame.Format == ShowMePixelFormat.Bgra8888 ? SKColorType.Bgra8888 : SKColorType.Rgba8888;
        var info = new SKImageInfo(frame.Width, frame.Height, colorType, SKAlphaType.Premul);
        using var bitmap = new SKBitmap();
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(frame.PixelData, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), frame.Stride);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public async Task PreviewServer_Renders_ColorsAxaml_As_Catalog()
    {
        var sampleAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/Colors.axaml";
        if (!File.Exists(sampleAxaml)) return;

        var resolveResult = await PreviewTargetResolver.ResolveAsync(sampleAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 1200, 800);

        var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
        server.XamlStatusReceived += x => xamlStatusTcs.TrySetResult(x);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
        Assert.Same(xamlStatusTcs.Task, completed);
        var status = await xamlStatusTcs.Task;
        Assert.True(status.Success, status.Error);

        var frame = await frameTcs.Task;
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);

        SaveFrameAsPng(frame, "/home/jmn/.gemini/antigravity-cli/brain/ad211c12-a9ed-4188-985c-58cd6efe2549/colors_catalog.png");
    }

    [Fact]
    public async Task PreviewServer_Renders_ColorsAxaml_In_TreeView_Mode()
    {
        var sampleAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/Colors.axaml";
        if (!File.Exists(sampleAxaml)) return;

        var resolveResult = await PreviewTargetResolver.ResolveAsync(sampleAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 1200, 800);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);
        var logs = new List<string>();
        server.LogReceived += l => logs.Add(l);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);
        var initialFrameCompleted = await Task.WhenAny(frameTcs.Task, Task.Delay(10000));
        Assert.Same(frameTcs.Task, initialFrameCompleted);

        var hitTcs = new TaskCompletionSource<HitTestResponseMessage>();
        server.HitTestResultReceived += res => hitTcs.TrySetResult(res);
        server.RequestHitTest(1125, 28);
        var hitCompleted = await Task.WhenAny(hitTcs.Task, Task.Delay(3000));
        if (hitCompleted == hitTcs.Task)
        {
            var hit = await hitTcs.Task;
            output.WriteLine($"[HitTest 1125,28] Found={hit.Found}, Type={hit.TypeName}, Element={hit.ElementName}");
        }

        var treeFrameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        ShowMeFramePacket? latestFrame = null;
        int frameCount = 0;
        server.FrameReceived += frame =>
        {
            frameCount++;
            latestFrame = frame;
            output.WriteLine($"[Test] Frame #{frameCount} received: {frame.Width}x{frame.Height}");
            if (frameCount >= 3)
            {
                treeFrameTcs.TrySetResult(frame);
            }
        };

        // Move over button, press and release
        server.SendPointerEvent(PointerActionType.Move, 1125, 28);
        await Task.Delay(150);
        server.SendPointerEvent(PointerActionType.Down, 1125, 28, PointerMouseButton.Left);
        await Task.Delay(150);
        server.SendPointerEvent(PointerActionType.Up, 1125, 28, PointerMouseButton.Left);

        var treeFrameCompleted = await Task.WhenAny(treeFrameTcs.Task, Task.Delay(6000));
        Assert.Same(treeFrameTcs.Task, treeFrameCompleted);

        var finalFrame = await treeFrameTcs.Task;
        output.WriteLine($"[Test] Total frames received: {frameCount}");
        output.WriteLine("HOST LOGS:\n" + string.Join("\n", logs));
        SaveFrameAsPng(finalFrame, "/home/jmn/.gemini/antigravity-cli/brain/ad211c12-a9ed-4188-985c-58cd6efe2549/colors_tree_catalog.png");
    }

    [Fact]
    public async Task PreviewServer_Renders_TypographyStylesAxaml_As_Catalog()
    {
        var sampleAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/TypographyStyles.axaml";
        if (!File.Exists(sampleAxaml)) return;

        var resolveResult = await PreviewTargetResolver.ResolveAsync(sampleAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 1200, 800);

        var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
        server.XamlStatusReceived += x => xamlStatusTcs.TrySetResult(x);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
        Assert.Same(xamlStatusTcs.Task, completed);
        var status = await xamlStatusTcs.Task;
        Assert.True(status.Success, status.Error);

        var frame = await frameTcs.Task;
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);

        SaveFrameAsPng(frame, "/home/jmn/.gemini/antigravity-cli/brain/ad211c12-a9ed-4188-985c-58cd6efe2549/typography_catalog.png");
    }

    [Fact]
    public async Task PreviewServer_Renders_DialogStylesAxaml_With_DesignPreviewWith()
    {
        var sampleAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/Controls/Dialogs/Styles.axaml";
        if (!File.Exists(sampleAxaml)) return;

        var resolveResult = await PreviewTargetResolver.ResolveAsync(sampleAxaml);
        Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

        var target = resolveResult.Value;
        using var server = new PreviewServer(target, 1200, 800);

        var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
        server.XamlStatusReceived += x => xamlStatusTcs.TrySetResult(x);

        var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
        server.FrameReceived += frame => frameTcs.TrySetResult(frame);

        var rawXaml = await File.ReadAllTextAsync(target.AxamlPath);
        await server.StartAsync(rawXaml);

        var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
        Assert.Same(xamlStatusTcs.Task, completed);
        var status = await xamlStatusTcs.Task;
        Assert.True(status.Success, status.Error);

        var frame = await frameTcs.Task;
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);

        SaveFrameAsPng(frame, "/home/jmn/.gemini/antigravity-cli/brain/ad211c12-a9ed-4188-985c-58cd6efe2549/dialogs_styles_preview.png");
    }
}
