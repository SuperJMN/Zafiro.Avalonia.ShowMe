using Avalonia.Media.Imaging;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class PreviewSessionResizeTests
{
    private class FakeStorageService : IStorageService
    {
        public Task<string?> OpenAxamlFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> SaveImageFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap) => Task.FromResult(true);
    }

    private PreviewSessionViewModel CreateSession(int initialWidth = 800, int initialHeight = 600)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test.axaml");
        if (!File.Exists(tempFile))
        {
            File.WriteAllText(tempFile, "<UserControl />");
        }

        var target = new PreviewTarget(
            tempFile,
            Path.GetTempPath(),
            Path.GetTempPath(),
            "dummy.dll",
            "dummy.dll",
            Path.GetTempPath(),
            "Dummy",
            "designer.dll",
            "dummy.runtimeconfig.json",
            "dummy.deps.json",
            "/test.axaml",
            initialWidth,
            initialHeight);

        var server = new PreviewServer(target, initialWidth, initialHeight);
        return new PreviewSessionViewModel(target, server, new FakeStorageService());
    }

    [Fact]
    public void InitialDimensions_Are_Set_From_Target()
    {
        using var session = CreateSession(600, 960);

        Assert.Equal(600, session.PreviewWidth);
        Assert.Equal(960, session.PreviewHeight);
        Assert.Equal("600 × 960 px", session.DimensionsText);
    }

    [Fact]
    public void OnResizeDrag_Updates_Dimensions_And_Sets_IsResizing()
    {
        using var session = CreateSession(600, 960);

        session.OnResizeDrag(750, 800);

        Assert.True(session.IsResizing);
        Assert.Equal(750, session.PreviewWidth);
        Assert.Equal(800, session.PreviewHeight);
        Assert.Equal("750 × 800 px", session.DimensionsText);
    }

    [Fact]
    public void OnResizeDrag_Clamps_To_Minimum_100()
    {
        using var session = CreateSession(600, 960);

        session.OnResizeDrag(20, 50);

        Assert.Equal(100, session.PreviewWidth);
        Assert.Equal(100, session.PreviewHeight);
    }

    [Fact]
    public void OnResizeDragCompleted_Resets_IsResizing()
    {
        using var session = CreateSession(600, 960);

        session.OnResizeDrag(750, 800);
        Assert.True(session.IsResizing);

        session.OnResizeDragCompleted();
        Assert.False(session.IsResizing);
    }

    [Fact]
    public void ResetDimensions_Restores_Target_Initial_Dimensions()
    {
        using var session = CreateSession(600, 960);

        session.OnResizeDrag(1200, 700);
        session.OnResizeDragCompleted();
        Assert.Equal(1200, session.PreviewWidth);
        Assert.Equal(700, session.PreviewHeight);

        session.ResetDimensions();

        Assert.Equal(600, session.PreviewWidth);
        Assert.Equal(960, session.PreviewHeight);
    }

    [Fact]
    public void ApplyPreset_Sets_Preset_Dimensions()
    {
        using var session = CreateSession(600, 960);

        var preset = session.Presets.First(p => p.Name.Contains("HD (720p)"));
        session.ApplyPreset(preset);

        Assert.Equal(1280, session.PreviewWidth);
        Assert.Equal(720, session.PreviewHeight);
    }

    [Fact]
    public void Presets_Include_Original_Design_Dimensions()
    {
        using var session = CreateSession(600, 960);

        var originalPreset = session.Presets.First(p => p.Name == "Diseño original");
        Assert.Equal(600, originalPreset.Width);
        Assert.Equal(960, originalPreset.Height);
    }
}
