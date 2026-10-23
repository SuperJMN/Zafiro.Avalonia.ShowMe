using CSharpFunctionalExtensions;
using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class EditorCommandTests
{
    private const string File = "/home/user/My Views/MainView.axaml";

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables)
    {
        var values = variables.ToDictionary(v => v.Name, v => v.Value);
        return name => values.GetValueOrDefault(name);
    }

    private static Func<string, Maybe<string>> Executables(params (string Name, string Path)[] executables)
    {
        var paths = executables.ToDictionary(e => e.Name, e => e.Path);
        return name => paths.TryGetValue(name, out var path) ? path : Maybe<string>.None;
    }

    [Fact]
    public void Template_placeholders_are_replaced_keeping_the_file_as_a_single_argument()
    {
        var command = EditorCommand.Resolve("rider --line {line} --column {column} {file}", Environment(), Executables(), File, 12, 7);

        Assert.Equal("rider", command.Value.Value.FileName);
        Assert.Equal(["--line", "12", "--column", "7", File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Quoted_template_parts_stay_together()
    {
        var command = EditorCommand.Resolve("\"/opt/My Editor/editor\" '+goto {line}:{column}' {file}", Environment(), Executables(), File, 3, 4);

        Assert.Equal("/opt/My Editor/editor", command.Value.Value.FileName);
        Assert.Equal(["+goto 3:4", File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Template_without_file_placeholder_gets_the_file_appended()
    {
        var command = EditorCommand.Resolve("gedit", Environment(), Executables(), File, 1, 1);

        Assert.Equal("gedit", command.Value.Value.FileName);
        Assert.Equal([File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Template_takes_precedence_over_environment()
    {
        var command = EditorCommand.Resolve("zed {file}:{line}", Environment(("VISUAL", "code"), ("EDITOR", "vim")), Executables(), File, 5, 2);

        Assert.Equal("zed", command.Value.Value.FileName);
        Assert.Equal([$"{File}:5"], command.Value.Value.Arguments);
    }

    [Fact]
    public void Visual_takes_precedence_over_editor()
    {
        var command = EditorCommand.Resolve(null, Environment(("VISUAL", "zed"), ("EDITOR", "code")), Executables(), File, 8, 3);

        Assert.Equal("zed", command.Value.Value.FileName);
        Assert.Equal([$"{File}:8:3"], command.Value.Value.Arguments);
    }

    [Fact]
    public void Editor_is_used_when_visual_is_not_set()
    {
        var command = EditorCommand.Resolve(null, Environment(("EDITOR", "/usr/bin/code --wait")), Executables(), File, 8, 3);

        Assert.Equal("/usr/bin/code", command.Value.Value.FileName);
        Assert.Equal(["--wait", "-g", $"{File}:8:3"], command.Value.Value.Arguments);
    }

    [Fact]
    public void Rider_from_environment_goes_to_line_and_column()
    {
        var command = EditorCommand.Resolve(null, Environment(("VISUAL", "rider")), Executables(), File, 20, 9);

        Assert.Equal(["--line", "20", "--column", "9", File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Terminal_editor_runs_inside_the_default_terminal_emulator_without_terminal_variable()
    {
        var command = EditorCommand.Resolve(null, Environment(("EDITOR", "nvim")), Executables(("x-terminal-emulator", "/usr/bin/x-terminal-emulator")), File, 4, 6);

        Assert.Equal("/usr/bin/x-terminal-emulator", command.Value.Value.FileName);
        Assert.Equal(["-e", "nvim", "+call cursor(4,6)", File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Terminal_editor_without_any_terminal_is_reported_instead_of_launched()
    {
        var command = EditorCommand.Resolve(null, Environment(("EDITOR", "nano")), Executables(("nano", "/usr/bin/nano")), File, 4, 6);

        Assert.True(command.IsFailure);
        Assert.Contains("nano necesita un terminal", command.Error);
    }

    [Fact]
    public void Terminal_editor_runs_inside_the_configured_terminal()
    {
        var command = EditorCommand.Resolve(null, Environment(("EDITOR", "vim"), ("TERMINAL", "kitty --single-instance")), Executables(), File, 4, 6);

        Assert.Equal("kitty", command.Value.Value.FileName);
        Assert.Equal(["--single-instance", "-e", "vim", "+call cursor(4,6)", File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Unknown_editor_from_environment_opens_the_file()
    {
        var command = EditorCommand.Resolve(null, Environment(("VISUAL", "kate")), Executables(), File, 4, 6);

        Assert.Equal("kate", command.Value.Value.FileName);
        Assert.Equal([File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Without_template_or_environment_the_system_handler_is_used()
    {
        var command = EditorCommand.Resolve(null, Environment(("VISUAL", " ")), Executables(), File, 4, 6);

        Assert.True(command.Value.HasNoValue);
    }

    [Fact]
    public void Template_program_is_launched_from_the_located_executable()
    {
        var command = EditorCommand.Resolve("code -g {file}:{line}:{column}", Environment(), Executables(("code", @"C:\Tools\VS Code\bin\code.cmd")), File, 1, 2);

        Assert.Equal(@"C:\Tools\VS Code\bin\code.cmd", command.Value.Value.FileName);
        Assert.Equal(["-g", $"{File}:1:2"], command.Value.Value.Arguments);
    }

    [Fact]
    public void Known_editor_from_environment_is_launched_from_the_located_executable()
    {
        var command = EditorCommand.Resolve(null, Environment(("EDITOR", "rider")), Executables(("rider", @"C:\JetBrains\scripts\rider.cmd")), File, 20, 9);

        Assert.Equal(@"C:\JetBrains\scripts\rider.cmd", command.Value.Value.FileName);
        Assert.Equal(["--line", "20", "--column", "9", File], command.Value.Value.Arguments);
    }

    [Fact]
    public void Start_info_passes_arguments_without_shell()
    {
        var startInfo = new EditorCommand("code", ["-g", $"{File}:1:2"]).ToStartInfo();

        Assert.Equal("code", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(["-g", $"{File}:1:2"], startInfo.ArgumentList);
    }
}
