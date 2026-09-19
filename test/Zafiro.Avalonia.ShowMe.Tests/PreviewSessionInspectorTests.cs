using Avalonia;
using Avalonia.Media.Imaging;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class PreviewSessionInspectorTests
{
    private class FakeStorageService : IStorageService
    {
        public Task<string?> OpenAxamlFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> SaveImageFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap) => Task.FromResult(true);
    }

    private PreviewSessionViewModel CreateSession()
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
            800,
            600);

        var server = new PreviewServer(target, 800, 600);
        var session = new PreviewSessionViewModel(target, server, new FakeStorageService());
        session.DispatchToUI = a => a();
        return session;
    }

    [Fact]
    public void Inspector_Is_Active_By_Default()
    {
        using var session = CreateSession();
        Assert.True(session.IsInspectorActive);
    }

    [Fact]
    public void HoverAdorner_Is_Visible_When_Element_Hovered()
    {
        using var session = CreateSession();
        Assert.False(session.IsHoverAdornerVisible);

        var hit = new ElementInspectionInfo(
            TypeName: "Button",
            ElementName: "SaveButton",
            LineNumber: 10,
            LinePosition: 5,
            SourceUri: null,
            Bounds: new Rect(10, 10, 100, 30),
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>()
        );

        // Setting hovered element via hit test simulation or reflection/property
        typeof(PreviewSessionViewModel).GetProperty(nameof(PreviewSessionViewModel.HoveredElement))?
            .SetValue(session, hit);

        Assert.True(session.IsHoverAdornerVisible);
        Assert.Equal("Button #SaveButton  100 × 30 px", session.HoveredElement?.HoverBadgeText);
    }

    [Fact]
    public void HoverAdorner_Hides_When_Hovered_Element_Matches_Selected_Element()
    {
        using var session = CreateSession();

        var elem = new ElementInspectionInfo(
            TypeName: "Button",
            ElementName: "SaveButton",
            LineNumber: 10,
            LinePosition: 5,
            SourceUri: null,
            Bounds: new Rect(10, 10, 100, 30),
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>()
        );

        typeof(PreviewSessionViewModel).GetProperty(nameof(PreviewSessionViewModel.SelectedElement))?
            .SetValue(session, elem);
        typeof(PreviewSessionViewModel).GetProperty(nameof(PreviewSessionViewModel.HoveredElement))?
            .SetValue(session, elem);

        // Should not duplicate adorner on top of already selected element
        Assert.False(session.IsHoverAdornerVisible);
    }

    [Fact]
    public void HoverAdorner_Shows_When_Hovering_Different_Element_While_Selected()
    {
        using var session = CreateSession();

        var selected = new ElementInspectionInfo(
            TypeName: "Grid",
            ElementName: "RootGrid",
            LineNumber: 1,
            LinePosition: 1,
            SourceUri: null,
            Bounds: new Rect(0, 0, 800, 600),
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>()
        );

        var hovered = new ElementInspectionInfo(
            TypeName: "TextBlock",
            ElementName: "TitleText",
            LineNumber: 5,
            LinePosition: 10,
            SourceUri: null,
            Bounds: new Rect(20, 20, 200, 40),
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>()
        );

        typeof(PreviewSessionViewModel).GetProperty(nameof(PreviewSessionViewModel.SelectedElement))?
            .SetValue(session, selected);
        typeof(PreviewSessionViewModel).GetProperty(nameof(PreviewSessionViewModel.HoveredElement))?
            .SetValue(session, hovered);

        Assert.True(session.IsHoverAdornerVisible);
        Assert.NotNull(session.SelectedElement);
    }

    [Fact]
    public void ClearHover_Hides_HoverAdorner()
    {
        using var session = CreateSession();

        var hovered = new ElementInspectionInfo(
            TypeName: "Button",
            ElementName: null,
            LineNumber: 12,
            LinePosition: 4,
            SourceUri: null,
            Bounds: new Rect(50, 50, 80, 30),
            Classes: [],
            Ancestors: [],
            Properties: new Dictionary<string, string>()
        );

        typeof(PreviewSessionViewModel).GetProperty(nameof(PreviewSessionViewModel.HoveredElement))?
            .SetValue(session, hovered);
        Assert.True(session.IsHoverAdornerVisible);

        session.ClearHover();
        Assert.Null(session.HoveredElement);
        Assert.False(session.IsHoverAdornerVisible);
    }

    [Fact]
    public void InspectMenuItemViewModel_Formats_DisplayText_Correctly()
    {
        var localItem = new InspectMenuItemViewModel(
            typeName: "TextBlock",
            elementName: "MyText",
            lineNumber: 15,
            linePosition: 4,
            sourceUri: null,
            resolvedFilePath: "/path/to/MainView.axaml",
            relativeFilePath: null, // local!
            bounds: new Rect(0, 0, 50, 20),
            onSelect: _ => { }
        );

        Assert.Equal("TextBlock #MyText [15:4]", localItem.DisplayText);
        Assert.Equal("15:4", localItem.LocationText);
        Assert.Equal("Abrir línea 15:4 en el editor", localItem.NavigationToolTip);

        var remoteItem = new InspectMenuItemViewModel(
            typeName: "WellStatusCardView",
            elementName: null,
            lineNumber: 81,
            linePosition: 1,
            sourceUri: "avares://App/Cards/Card.axaml",
            resolvedFilePath: "/path/to/Cards/Card.axaml",
            relativeFilePath: "Cards/Card.axaml", // remote!
            bounds: new Rect(10, 10, 200, 100),
            onSelect: _ => { }
        );

        Assert.Equal("WellStatusCardView (Cards/Card.axaml) [81:1]", remoteItem.DisplayText);
        Assert.Equal("Cards/Card.axaml", remoteItem.RelativeFilePath);
        Assert.Equal("Abrir Cards/Card.axaml:81 en el editor", remoteItem.NavigationToolTip);
    }

    [Fact]
    public void ContextMenuHitTestResponse_Triggers_RequestShowContextMenu()
    {
        using var session = CreateSession();

        Point? triggeredPoint = null;
        IReadOnlyList<InspectMenuItemViewModel>? triggeredItems = null;

        session.RequestShowContextMenu += (pt, items) =>
        {
            triggeredPoint = pt;
            triggeredItems = items;
        };

        var response = new Zafiro.Avalonia.ShowMe.Protocol.ContextMenuHitTestResponseMessage(
            RequestId: "test-req",
            X: 120,
            Y: 250,
            Items:
            [
                new Zafiro.Avalonia.ShowMe.Protocol.VisualItemInfo("TextBlock", "Title", 10, 2, null, 120, 250, 80, 20),
                new Zafiro.Avalonia.ShowMe.Protocol.VisualItemInfo("StackPanel", null, 8, 1, null, 100, 240, 200, 100)
            ]
        );

        // Simulate receiving the message via reflection on private handler
        var method = typeof(PreviewSessionViewModel).GetMethod(
            "OnContextMenuHitTestResultReceived",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        method?.Invoke(session, [response]);

        Assert.NotNull(triggeredPoint);
        Assert.Equal(120, triggeredPoint.Value.X);
        Assert.Equal(250, triggeredPoint.Value.Y);
        Assert.NotNull(triggeredItems);
        Assert.Equal(2, triggeredItems.Count);
        Assert.Equal("TextBlock", triggeredItems[0].TypeName);
        Assert.Equal("StackPanel", triggeredItems[1].TypeName);
    }
}
