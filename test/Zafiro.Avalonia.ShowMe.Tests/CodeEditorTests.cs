using System.Diagnostics;
using CSharpFunctionalExtensions;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public sealed class CodeEditorTests : IDisposable
{
    private const string File = "/home/user/Views/MainView.axaml";

    private readonly string settingsPath = Path.Combine(Path.GetTempPath(), $"showme-settings-{Guid.NewGuid():N}", "settings.json");
    private readonly List<ProcessStartInfo> started = [];

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(settingsPath)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private CodeEditor Editor(string? commandLine = null, params (string Name, string Value)[] environment)
    {
        var variables = environment.ToDictionary(v => v.Name, v => v.Value);
        return new CodeEditor(
            commandLine,
            new JsonUserSettingsStore(settingsPath),
            name => variables.GetValueOrDefault(name),
            _ => Maybe<string>.None,
            started.Add);
    }

    [Fact]
    public void Without_any_editor_open_in_editor_is_not_available_and_nothing_is_launched()
    {
        var editor = Editor();

        var opened = editor.Open(File, 3, 4);

        Assert.False(editor.IsAvailable);
        Assert.True(opened.IsFailure);
        Assert.Contains("no hay ningún editor", opened.Error);
        Assert.Empty(started);
    }

    [Fact]
    public void Editor_saved_from_the_app_applies_to_later_runs_without_command_line_option()
    {
        Editor().Save("zed {file}:{line}");

        var nextRun = Editor();
        nextRun.Open(File, 12, 1);

        Assert.True(nextRun.IsAvailable);
        Assert.Equal("zed", Assert.Single(started).FileName);
        Assert.Equal([$"{File}:12"], started[0].ArgumentList);
    }

    [Fact]
    public void Saving_an_editor_makes_open_in_editor_available_right_away()
    {
        var editor = Editor();

        editor.Save("zed {file}");

        Assert.True(editor.IsAvailable);
    }

    [Fact]
    public void Command_line_editor_takes_precedence_over_the_saved_one()
    {
        Editor().Save("zed {file}");

        Editor("rider --line {line} {file}").Open(File, 7, 1);

        Assert.Equal("rider", Assert.Single(started).FileName);
    }

    [Fact]
    public void Saved_editor_takes_precedence_over_visual_and_editor()
    {
        Editor().Save("zed {file}");

        Editor(null, ("VISUAL", "code"), ("EDITOR", "vim")).Open(File, 7, 1);

        Assert.Equal("zed", Assert.Single(started).FileName);
    }

    [Fact]
    public void Environment_editor_is_used_when_nothing_else_is_configured()
    {
        var editor = Editor(null, ("VISUAL", "code"));

        editor.Open(File, 7, 2);

        Assert.True(editor.IsAvailable);
        Assert.Equal("code", Assert.Single(started).FileName);
        Assert.Equal(["-g", $"{File}:7:2"], started[0].ArgumentList);
    }

    [Fact]
    public void Clearing_the_saved_editor_falls_back_to_the_environment()
    {
        var editor = Editor(null, ("EDITOR", "code"));
        editor.Save("zed {file}");

        editor.Save("  ");
        editor.Open(File, 1, 1);

        Assert.Null(Editor().SavedTemplate);
        Assert.Equal("code", Assert.Single(started).FileName);
    }

    [Fact]
    public void Editor_that_cannot_be_started_is_reported()
    {
        var editor = new CodeEditor(
            "missing-editor {file}",
            new JsonUserSettingsStore(settingsPath),
            _ => null,
            _ => Maybe<string>.None,
            _ => throw new System.ComponentModel.Win32Exception("No such file or directory"));

        var opened = editor.Open(File, 1, 1);

        Assert.True(opened.IsFailure);
        Assert.Contains("no se pudo iniciar missing-editor", opened.Error);
    }
}
