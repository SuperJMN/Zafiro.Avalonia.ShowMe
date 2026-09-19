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
    public async Task PreviewServer_Renders_GradientsAxaml_As_Catalog()
    {
        var sampleAxaml = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/Gradients.axaml";
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
        SaveFrameAsPng(frame, "/home/jmn/.gemini/antigravity-cli/brain/ad211c12-a9ed-4188-985c-58cd6efe2549/gradients_catalog.png");
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

    }

    [Fact]
    public async Task PreviewServer_Renders_Geometries_As_Catalog()
    {
        var targetDir = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens";
        if (!Directory.Exists(targetDir)) return;

        var tempAxaml = Path.Combine(targetDir, "IconsTest.axaml");
        var xamlContent = """
        <ResourceDictionary xmlns="https://github.com/avaloniaui"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <StreamGeometry x:Key="IconCheck">M 9 16.17 L 4.83 12 L 3.41 13.41 L 9 19 L 21 7 L 19.59 5.59 L 9 16.17 Z</StreamGeometry>
            <StreamGeometry x:Key="IconClose">M 19 6.41 L 17.59 5 L 12 10.59 L 6.41 5 L 5 6.41 L 10.59 12 L 5 17.59 L 6.41 19 L 12 13.41 L 17.59 19 L 19 17.59 L 13.41 12 Z</StreamGeometry>
            <StreamGeometry x:Key="IconSearch">M 15.5 14 h -0.79 l -0.28 -0.27 C 15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3 S 3 5.91 3 9.5 5.91 16 9.5 16 c 1.61 0 3.09 -0.59 4.23 -1.57 l 0.27 0.28 v 0.79 l 5 4.99 L 20.49 19 l -4.99 -5 z m -6 0 C 7.01 14 5 11.99 5 9.5 S 7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14 z</StreamGeometry>
            <StreamGeometry x:Key="IconSettings">M 19.14 12.94 c 0.04 -0.3 0.06 -0.61 0.06 -0.94 c 0 -0.32 -0.02 -0.64 -0.07 -0.94 l 2.03 -1.58 c 0.18 -0.14 0.23 -0.41 0.12 -0.61 l -1.92 -3.32 c -0.12 -0.22 -0.37 -0.29 -0.59 -0.22 l -2.39 0.96 c -0.5 -0.38 -1.03 -0.7 -1.62 -0.94 L 14.4 2.81 c -0.04 -0.24 -0.24 -0.41 -0.48 -0.41 h -3.84 c -0.24 0 -0.43 0.17 -0.47 0.41 L 9.25 5.35 C 8.66 5.59 8.12 5.92 7.63 6.29 L 5.24 5.33 c -0.22 -0.08 -0.47 0 -0.59 0.22 L 2.74 8.87 c -0.12 0.21 -0.08 0.47 0.12 0.61 l 2.03 1.58 c -0.05 0.3 -0.09 0.63 -0.09 0.94 s 0.02 0.64 0.07 0.94 l -2.03 1.58 c -0.18 0.14 -0.23 0.41 -0.12 0.61 l 1.92 3.32 c 0.12 0.22 0.37 0.29 0.59 0.22 l 2.39 -0.96 c 0.5 0.38 1.03 0.7 1.62 0.94 l 0.36 2.54 c 0.05 0.24 0.24 0.41 0.48 0.41 h 3.84 c 0.24 0 0.44 -0.17 0.47 -0.41 l 0.36 -2.54 c 0.59 -0.24 1.13 -0.56 1.62 -0.94 l 2.39 0.96 c 0.22 0.08 0.47 0 0.59 -0.22 l 1.92 -3.32 c 0.12 -0.22 0.07 -0.47 -0.12 -0.61 L 19.14 12.94 z M 12 15.6 c -1.98 0 -3.6 -1.62 -3.6 -3.6 s 1.62 -3.6 3.6 -3.6 s 3.6 1.62 3.6 3.6 s -1.62 3.6 -3.6 3.6 z</StreamGeometry>
        </ResourceDictionary>
        """;
        try
        {
            await File.WriteAllTextAsync(tempAxaml, xamlContent);

            var resolveResult = await PreviewTargetResolver.ResolveAsync(tempAxaml);
            Assert.True(resolveResult.IsSuccess, resolveResult.IsFailure ? resolveResult.Error : "");

            var target = resolveResult.Value;
            using var server = new PreviewServer(target, 1200, 800);

            var xamlStatusTcs = new TaskCompletionSource<XamlStatusMessage>();
            server.XamlStatusReceived += x => xamlStatusTcs.TrySetResult(x);

            var frameTcs = new TaskCompletionSource<ShowMeFramePacket>();
            server.FrameReceived += frame => frameTcs.TrySetResult(frame);

            await server.StartAsync(xamlContent);

            var completed = await Task.WhenAny(xamlStatusTcs.Task, Task.Delay(15000));
            Assert.Same(xamlStatusTcs.Task, completed);
            var status = await xamlStatusTcs.Task;
            Assert.True(status.Success, status.Error);

            var frame = await frameTcs.Task;
            Assert.NotNull(frame);
            SaveFrameAsPng(frame, "/home/jmn/.gemini/antigravity-cli/brain/ad211c12-a9ed-4188-985c-58cd6efe2549/geometries_catalog.png");
        }
        finally
        {
            if (File.Exists(tempAxaml))
            {
                File.Delete(tempAxaml);
            }
        }
    }
}
