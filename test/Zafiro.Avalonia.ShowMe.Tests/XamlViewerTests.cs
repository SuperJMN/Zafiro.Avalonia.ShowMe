using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using CSharpFunctionalExtensions;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Tests;

public sealed class XamlViewerTests : IDisposable
{
    private const string PreviewedXaml = "<UserControl>\n  <StackPanel>\n    <Button x:Name=\"SaveButton\" />\n  </StackPanel>\n</UserControl>";
    private const string CardXaml = "<UserControl>\n  <Border>\n    <TextBlock Text=\"Card\" />\n  </Border>\n</UserControl>";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"showme-viewer-{Guid.NewGuid():N}");
    private readonly string previewedFile;
    private readonly string cardFile;
    private readonly string settingsPath;
    private readonly List<ProcessStartInfo> started = [];

    public XamlViewerTests()
    {
        Directory.CreateDirectory(directory);
        previewedFile = Path.Combine(directory, "MainView.axaml");
        cardFile = Path.Combine(directory, "CardView.axaml");
        settingsPath = Path.Combine(directory, "settings.json");
        File.WriteAllText(previewedFile, PreviewedXaml);
        File.WriteAllText(cardFile, CardXaml);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private sealed class FakeStorageService : IStorageService
    {
        public Task<string?> OpenAxamlFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> SaveImageFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap) => Task.FromResult(true);
    }

    private CodeEditor Editor(Action<ProcessStartInfo>? start = null) => new(
        null,
        new JsonUserSettingsStore(settingsPath),
        _ => null,
        _ => Maybe<string>.None,
        start ?? started.Add);

    private PreviewSessionViewModel Session(CodeEditor? editor = null)
    {
        var target = new PreviewTarget(
            previewedFile, directory, directory, "dummy.dll", "dummy.dll", directory, "Dummy",
            "designer.dll", "dummy.runtimeconfig.json", "dummy.deps.json", "/MainView.axaml", 800, 600);
        var session = new PreviewSessionViewModel(target, new PreviewServer(target, 800, 600), new FakeStorageService(), codeEditor: editor);
        session.DispatchToUI = a => a();
        return session;
    }

    private static void Receive(PreviewSessionViewModel session, ShowMeMessage message)
    {
        var handler = message is HitTestResponseMessage ? "OnHitTestResultReceived" : "OnContextMenuHitTestResultReceived";
        typeof(PreviewSessionViewModel)
            .GetMethod(handler, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(session, [message]);
    }

    private static HitTestResponseMessage SaveButtonFound() =>
        new("req", Found: true, TypeName: "Button", ElementName: "SaveButton", LineNumber: 3, LinePosition: 5,
            BoundsX: 10, BoundsY: 20, BoundsWidth: 100, BoundsHeight: 30);

    private ContextMenuHitTestResponseMessage MenuWithCardText() =>
        new("req", 50, 60,
        [
            new VisualItemInfo("TextBlock", null, 3, 5, new Uri(cardFile).AbsoluteUri, 50, 60, 80, 20),
            new VisualItemInfo("StackPanel", null, 2, 3, null, 0, 0, 200, 100)
        ]);

    private IReadOnlyList<InspectMenuItemViewModel> OpenContextMenu(PreviewSessionViewModel session)
    {
        IReadOnlyList<InspectMenuItemViewModel> shown = [];
        session.RequestShowContextMenu += (_, items) => shown = items;
        Receive(session, MenuWithCardText());
        return shown;
    }

    [Fact]
    public void View_xaml_shows_the_previewed_file_at_the_line_of_the_selected_element()
    {
        using var session = Session();
        Receive(session, SaveButtonFound());

        session.ViewXamlCommand.Execute(null);

        Assert.Equal(previewedFile, session.SourceView?.FilePath);
        Assert.Equal(PreviewedXaml, session.SourceView?.Text);
        Assert.Equal(3, session.SourceView?.LineNumber);
        Assert.Equal("MainView.axaml:3", session.SourceView?.LocationText);
    }

    [Fact]
    public void View_xaml_needs_a_selected_element()
    {
        using var session = Session();

        Assert.False(session.ViewXamlCommand.CanExecute(null));
    }

    [Fact]
    public void Element_defined_in_another_file_shows_that_file()
    {
        using var session = Session();
        var cardText = OpenContextMenu(session)[0];

        cardText.ViewXamlCommand.Execute(null);

        Assert.Equal(cardFile, session.SourceView?.FilePath);
        Assert.Equal(CardXaml, session.SourceView?.Text);
        Assert.Equal(3, session.SourceView?.LineNumber);
    }

    [Fact]
    public void View_xaml_from_the_context_menu_also_selects_the_element()
    {
        using var session = Session();
        var cardText = OpenContextMenu(session)[0];

        cardText.ViewXamlCommand.Execute(null);

        Assert.Equal("TextBlock", session.SelectedElement?.TypeName);
        Assert.Equal(new Rect(50, 60, 80, 20), session.SelectionBounds);
    }

    [Fact]
    public void Double_click_on_an_element_shows_its_xaml()
    {
        using var session = Session();
        session.IsInspectorActive = true;
        session.OnPointerInput(PointerActionType.Down, new Point(15, 25), PointerMouseButton.Left, default, false, false, false, clickCount: 1);
        session.OnPointerInput(PointerActionType.Down, new Point(15, 25), PointerMouseButton.Left, default, false, false, false, clickCount: 2);

        Receive(session, SaveButtonFound());

        Assert.Equal(previewedFile, session.SourceView?.FilePath);
        Assert.Equal(3, session.SourceView?.LineNumber);
    }

    [Fact]
    public void Single_click_selects_without_showing_the_xaml()
    {
        using var session = Session();
        session.IsInspectorActive = true;
        session.OnPointerInput(PointerActionType.Down, new Point(15, 25), PointerMouseButton.Left, default, false, false, false, clickCount: 1);

        Receive(session, SaveButtonFound());

        Assert.Equal("Button", session.SelectedElement?.TypeName);
        Assert.Null(session.SourceView);
    }

    [Fact]
    public void Viewer_can_be_closed()
    {
        using var session = Session();
        Receive(session, SaveButtonFound());
        session.ViewXamlCommand.Execute(null);

        session.CloseSourceViewCommand.Execute(null);

        Assert.Null(session.SourceView);
    }

    [Fact]
    public void Escape_closes_the_viewer_before_clearing_the_selection()
    {
        using var session = Session();
        Receive(session, SaveButtonFound());
        session.ViewXamlCommand.Execute(null);

        session.HandleEscape();

        Assert.Null(session.SourceView);
        Assert.Equal("Button", session.SelectedElement?.TypeName);
    }

    [Fact]
    public void Unreadable_file_is_reported_in_the_status_bar()
    {
        using var session = Session();

        session.ViewXaml(Path.Combine(directory, "Missing.axaml"), 1, 1);

        Assert.Null(session.SourceView);
        Assert.StartsWith("No se pudo leer Missing.axaml", session.Status);
    }

    [Fact]
    public void Open_in_editor_is_not_offered_without_an_editor()
    {
        using var session = Session(Editor());
        Receive(session, SaveButtonFound());

        Assert.False(session.CanOpenInEditor);
        Assert.False(session.OpenInEditorCommand.CanExecute(null));
        Assert.False(OpenContextMenu(session)[0].CanOpenInEditor);
    }

    [Fact]
    public void Open_in_editor_launches_the_configured_editor_at_the_element_line()
    {
        var editor = Editor();
        editor.Save("zed {file}:{line}:{column}");
        using var session = Session(editor);
        Receive(session, SaveButtonFound());

        session.OpenInEditorCommand.Execute(null);

        Assert.Equal("zed", Assert.Single(started).FileName);
        Assert.Equal([$"{previewedFile}:3:5"], started[0].ArgumentList);
        Assert.Equal("Abriendo MainView.axaml:3 con zed.", session.Status);
    }

    [Fact]
    public void Context_menu_offers_open_in_editor_when_an_editor_is_configured()
    {
        var editor = Editor();
        editor.Save("zed {file}:{line}");
        using var session = Session(editor);
        var cardText = OpenContextMenu(session)[0];

        cardText.OpenInEditorCommand.Execute(null);

        Assert.True(cardText.CanOpenInEditor);
        Assert.Equal([$"{cardFile}:3"], Assert.Single(started).ArgumentList);
    }

    [Fact]
    public void Editor_failure_is_reported_in_the_status_bar()
    {
        var editor = Editor(_ => throw new System.ComponentModel.Win32Exception("No such file or directory"));
        editor.Save("missing-editor {file}");
        using var session = Session(editor);
        Receive(session, SaveButtonFound());

        session.OpenInEditorCommand.Execute(null);

        Assert.StartsWith("No se pudo abrir MainView.axaml: no se pudo iniciar missing-editor", session.Status);
    }

    [Fact]
    public void Configuring_the_editor_from_the_app_offers_open_in_editor_in_the_open_session()
    {
        var editor = Editor();
        using var session = Session(editor);
        var main = new MainViewModel(new FakeStorageService(), codeEditor: editor);

        main.ConfigureEditorCommand.Execute(null);
        main.EditorSettings!.Command = "zed {file}:{line}";
        main.EditorSettings.SaveCommand.Execute(null);

        Assert.Null(main.EditorSettings);
        Assert.True(session.CanOpenInEditor);
        Assert.Equal("zed {file}:{line}", Editor().SavedTemplate);
    }

    [Fact]
    public void Cancelling_the_editor_dialog_keeps_the_saved_editor()
    {
        var editor = Editor();
        editor.Save("zed {file}");
        var main = new MainViewModel(new FakeStorageService(), codeEditor: editor);

        main.ConfigureEditorCommand.Execute(null);
        main.EditorSettings!.Command = "code -g {file}";
        main.EditorSettings.CancelCommand.Execute(null);

        Assert.Null(main.EditorSettings);
        Assert.Equal("zed {file}", Editor().SavedTemplate);
    }
}
