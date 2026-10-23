using Avalonia;
using Avalonia.Media.Imaging;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Tests;

public sealed class OpenDefiningViewTests : IDisposable
{
    private const string Namespaces = "xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"showme-open-view-{Guid.NewGuid():N}");
    private readonly string previewedFile;
    private readonly string cardFile;
    private readonly string dictionaryFile;
    private readonly string appFile;
    private readonly List<string> openedViews = [];

    public OpenDefiningViewTests()
    {
        Directory.CreateDirectory(directory);
        previewedFile = Write("MainView.axaml", $"<UserControl {Namespaces}>\n  <Button x:Name=\"SaveButton\" />\n</UserControl>");
        cardFile = Write("CardView.axaml", $"<UserControl {Namespaces}>\n  <TextBlock Text=\"Card\" />\n</UserControl>");
        dictionaryFile = Write("Theme.axaml", $"<ResourceDictionary {Namespaces}>\n  <ControlTheme x:Key=\"Card\" TargetType=\"Border\" />\n</ResourceDictionary>");
        appFile = Write("App.axaml", $"<Application {Namespaces}>\n  <Application.Styles />\n</Application>");
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class FakeStorageService : IStorageService
    {
        public Task<string?> OpenAxamlFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> SaveImageFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap) => Task.FromResult(true);
    }

    private PreviewSessionViewModel Session(bool canNavigate = true)
    {
        var target = new PreviewTarget(
            previewedFile, directory, directory, "dummy.dll", "dummy.dll", directory, "Dummy",
            "designer.dll", "dummy.runtimeconfig.json", "dummy.deps.json", "/MainView.axaml", 800, 600);
        Func<string, Task>? openView = canNavigate
            ? path =>
            {
                openedViews.Add(path);
                return Task.CompletedTask;
            }
            : null;
        var session = new PreviewSessionViewModel(target, new PreviewServer(target, 800, 600), new FakeStorageService(), openView: openView);
        session.DispatchToUI = a => a();
        return session;
    }

    private static void Receive(PreviewSessionViewModel session, ShowMeMessage message)
    {
        var handler = message switch
        {
            HitTestResponseMessage => "OnHitTestResultReceived",
            FindElementResponseMessage => "OnFindElementResultReceived",
            _ => "OnContextMenuHitTestResultReceived"
        };
        typeof(PreviewSessionViewModel)
            .GetMethod(handler, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(session, [message]);
    }

    private static HitTestResponseMessage Selected(string? sourceUri) =>
        new("req", Found: true, TypeName: "TextBlock", LineNumber: 2, LinePosition: 3, SourceUri: sourceUri,
            BoundsX: 10, BoundsY: 20, BoundsWidth: 100, BoundsHeight: 30);

    private ContextMenuHitTestResponseMessage MenuOver(params (string Type, string? SourceUri, Rect Bounds)[] items) =>
        new("req", 50, 60, items
            .Select(i => new VisualItemInfo(i.Type, null, 2, 3, i.SourceUri, i.Bounds.X, i.Bounds.Y, i.Bounds.Width, i.Bounds.Height))
            .ToList());

    private static string Uri(string file) => new Uri(file).AbsoluteUri;

    [Fact]
    public void Element_defined_in_another_view_can_open_that_view()
    {
        using var session = Session();
        Receive(session, Selected(Uri(cardFile)));

        session.OpenDefiningViewCommand.Execute(null);

        Assert.True(session.CanOpenDefiningView);
        Assert.Equal([cardFile], openedViews);
    }

    [Fact]
    public void Element_of_the_previewed_file_does_not_offer_open_view()
    {
        using var session = Session();
        Receive(session, Selected(Uri(previewedFile)));

        Assert.False(session.CanOpenDefiningView);
        Assert.False(session.OpenDefiningViewCommand.CanExecute(null));
    }

    [Fact]
    public void Element_defined_in_a_resource_dictionary_opens_it_as_the_resource_catalog()
    {
        using var session = Session();
        Receive(session, Selected(Uri(dictionaryFile)));

        session.OpenDefiningViewCommand.Execute(null);

        Assert.Equal([dictionaryFile], openedViews);
    }

    [Fact]
    public void Element_defined_in_the_application_does_not_offer_open_view()
    {
        using var session = Session();
        Receive(session, Selected(Uri(appFile)));

        Assert.False(session.CanOpenDefiningView);
    }

    [Fact]
    public void Element_whose_file_is_not_on_disk_does_not_offer_open_view()
    {
        using var session = Session();
        Receive(session, Selected(Uri(Path.Combine(directory, "Missing.axaml"))));

        Assert.False(session.CanOpenDefiningView);
    }

    [Fact]
    public void Open_view_is_not_offered_without_navigation()
    {
        using var session = Session(canNavigate: false);
        Receive(session, Selected(Uri(cardFile)));

        Assert.False(session.CanOpenDefiningView);
    }

    [Fact]
    public void Open_view_is_offered_only_for_context_menu_items_from_other_views()
    {
        using var session = Session();
        IReadOnlyList<InspectMenuItemViewModel> shown = [];
        session.RequestShowContextMenu += (_, items) => shown = items;

        Receive(session, MenuOver(
            ("TextBlock", Uri(cardFile), new Rect(50, 60, 80, 20)),
            ("Border", Uri(appFile), new Rect(40, 50, 100, 40)),
            ("StackPanel", null, new Rect(0, 0, 200, 100))));

        Assert.Equal(new[] { true, false, false }, shown.Select(item => item.CanOpenView));
    }

    [Fact]
    public void Open_view_from_the_context_menu_selects_the_element_and_opens_its_view()
    {
        using var session = Session();
        IReadOnlyList<InspectMenuItemViewModel> shown = [];
        session.RequestShowContextMenu += (_, items) => shown = items;
        Receive(session, MenuOver(("TextBlock", Uri(cardFile), new Rect(50, 60, 80, 20))));

        shown[0].OpenViewCommand.Execute(null);

        Assert.Equal("TextBlock", session.SelectedElement?.TypeName);
        Assert.Equal([cardFile], openedViews);
    }

    private ElementInspectionInfo PreviouslySelected() =>
        new("TextBlock", null, 2, 3, Uri(cardFile), new Rect(50, 60, 80, 20), [], [], new Dictionary<string, string>(), TreePath: "0/1/0");

    private static FindElementResponseMessage FoundAt(ElementInspectionInfo element, Rect bounds) =>
        new("req", new VisualItemInfo(element.TypeName, element.ElementName, element.LineNumber, element.LinePosition, element.SourceUri,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, element.TreePath));

    [Fact]
    public void Restored_selection_is_the_same_element_where_it_is_now()
    {
        using var session = Session();
        var previous = PreviouslySelected();

        session.RestoreSelection(previous);
        Receive(session, FoundAt(previous, new Rect(300, 450, 80, 20)));

        Assert.Equal(previous with { Bounds = new Rect(300, 450, 80, 20) }, session.SelectedElement);
        Assert.Equal(new Rect(300, 450, 80, 20), session.SelectionBounds);
    }

    [Fact]
    public void Selection_is_not_restored_when_the_element_is_gone()
    {
        using var session = Session();

        session.RestoreSelection(PreviouslySelected());
        Receive(session, new FindElementResponseMessage("req", null));

        Assert.Null(session.SelectedElement);
    }

    [Fact]
    public void A_selection_made_before_the_preview_answers_is_kept()
    {
        using var session = Session();
        var previous = PreviouslySelected();

        session.RestoreSelection(previous);
        Receive(session, Selected(Uri(previewedFile)));
        Receive(session, FoundAt(previous, new Rect(300, 450, 80, 20)));

        Assert.Equal(Uri(previewedFile), session.SelectedElement?.SourceUri);
    }
}
