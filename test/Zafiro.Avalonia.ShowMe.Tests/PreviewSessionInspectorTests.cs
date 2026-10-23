using Avalonia;
using Avalonia.Media.Imaging;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Protocol;
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

    private static InspectMenuItemViewModel CreateMenuItem(string typeName, Rect bounds, Action<InspectMenuItemViewModel> onSelect) =>
        new(
            typeName: typeName,
            elementName: null,
            lineNumber: 10,
            linePosition: 2,
            sourceUri: null,
            resolvedFilePath: "/path/to/MainView.axaml",
            relativeFilePath: null,
            bounds: bounds,
            onSelect: onSelect
        );

    private static void ReceiveHitTestResponse(PreviewSessionViewModel session, HitTestResponseMessage response)
    {
        var method = typeof(PreviewSessionViewModel).GetMethod(
            "OnHitTestResultReceived",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );
        method?.Invoke(session, [response]);
    }

    private static HitTestResponseMessage FoundButton(bool isHover) =>
        new("test-req", Found: true, TypeName: "Button", ElementName: "SaveButton", LineNumber: 10, LinePosition: 5,
            BoundsX: 10, BoundsY: 20, BoundsWidth: 100, BoundsHeight: 30, IsHover: isHover);

    [Fact]
    public void Inspector_Is_Off_By_Default()
    {
        using var session = CreateSession();
        Assert.False(session.IsInspectorActive);
    }

    [Fact]
    public void ToggleInspectorCommand_Turns_Pick_Mode_On_And_Off()
    {
        using var session = CreateSession();

        session.ToggleInspectorCommand.Execute(null);
        Assert.True(session.IsInspectorActive);

        session.ToggleInspectorCommand.Execute(null);
        Assert.False(session.IsInspectorActive);
    }

    [Fact]
    public void Click_In_Pick_Mode_Leaves_Pick_Mode()
    {
        using var session = CreateSession();
        session.IsInspectorActive = true;

        session.OnPointerInput(PointerActionType.Down, new Point(15, 25), PointerMouseButton.Left, default, false, false, false);

        Assert.False(session.IsInspectorActive);
    }

    [Fact]
    public void Hit_Test_Response_For_Click_Selects_Element()
    {
        using var session = CreateSession();

        ReceiveHitTestResponse(session, FoundButton(isHover: false));

        Assert.Equal("Button", session.SelectedElement?.TypeName);
        Assert.Equal(new Rect(10, 20, 100, 30), session.SelectionBounds);
    }

    [Fact]
    public void Hover_Response_Highlights_Element_In_Pick_Mode()
    {
        using var session = CreateSession();
        session.IsInspectorActive = true;

        ReceiveHitTestResponse(session, FoundButton(isHover: true));

        Assert.Equal("Button", session.HoveredElement?.TypeName);
        Assert.True(session.IsHoverAdornerVisible);
    }

    [Fact]
    public void Hover_Response_Arriving_After_Leaving_Pick_Mode_Is_Ignored()
    {
        using var session = CreateSession();

        ReceiveHitTestResponse(session, FoundButton(isHover: true));

        Assert.Null(session.HoveredElement);
        Assert.False(session.IsHoverAdornerVisible);
    }

    [Fact]
    public void Selection_Persists_When_Leaving_Pick_Mode()
    {
        using var session = CreateSession();
        ReceiveHitTestResponse(session, FoundButton(isHover: false));
        session.IsInspectorActive = true;

        session.IsInspectorActive = false;

        Assert.Equal("Button", session.SelectedElement?.TypeName);
        Assert.Equal(new Rect(10, 20, 100, 30), session.SelectionBounds);
    }

    [Fact]
    public void Escape_In_Pick_Mode_Cancels_It_And_Keeps_Selection()
    {
        using var session = CreateSession();
        ReceiveHitTestResponse(session, FoundButton(isHover: false));
        session.IsInspectorActive = true;

        var handled = session.HandleEscape();

        Assert.True(handled);
        Assert.False(session.IsInspectorActive);
        Assert.Equal("Button", session.SelectedElement?.TypeName);
    }

    [Fact]
    public void Escape_Outside_Pick_Mode_Clears_Selection()
    {
        using var session = CreateSession();
        ReceiveHitTestResponse(session, FoundButton(isHover: false));

        var handled = session.HandleEscape();

        Assert.True(handled);
        Assert.Null(session.SelectedElement);
        Assert.Null(session.SelectionBounds);
    }

    [Fact]
    public void Escape_Without_Pick_Mode_Or_Selection_Is_Left_To_The_Preview()
    {
        using var session = CreateSession();

        Assert.False(session.HandleEscape());
    }

    [Fact]
    public void Choosing_Context_Menu_Item_Selects_And_Highlights_It()
    {
        using var session = CreateSession();
        session.IsInspectorActive = true;
        var item = CreateMenuItem("TextBlock", new Rect(120, 250, 80, 20), session.SelectElement);

        item.SelectCommand.Execute(null);

        Assert.Equal("TextBlock", session.SelectedElement?.TypeName);
        Assert.Equal(new Rect(120, 250, 80, 20), session.SelectionBounds);
        Assert.False(session.IsInspectorActive);
    }

    [Fact]
    public void Hovering_Context_Menu_Item_Highlights_It_With_Pick_Mode_Off()
    {
        using var session = CreateSession();
        var item = CreateMenuItem("StackPanel", new Rect(0, 0, 200, 100), _ => { });

        session.HoverElement(item);

        Assert.True(session.IsHoverAdornerVisible);
        Assert.Equal("StackPanel", session.HoveredElement?.TypeName);
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
