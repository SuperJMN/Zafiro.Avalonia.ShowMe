using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class ExecutableLocatorTests
{
    private static Func<string, string?> Environment(params (string Name, string Value)[] variables)
    {
        var values = variables.ToDictionary(v => v.Name, v => v.Value);
        return name => values.GetValueOrDefault(name);
    }

    private static Func<string, bool> Files(params string[] paths)
    {
        return paths.Contains;
    }

    [Fact]
    public void Windows_command_without_extension_resolves_to_its_cmd_script()
    {
        var environment = Environment(("PATH", @"C:\Windows\system32;""C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin\"""), ("PATHEXT", ".COM;.EXE;.BAT;.CMD"));

        var path = ExecutableLocator.Find("code", environment, Files(@"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd"), isWindows: true);

        Assert.Equal(@"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd", path.Value);
    }

    [Fact]
    public void Windows_follows_path_order_and_then_pathext_order()
    {
        var environment = Environment(("PATH", @"C:\first;C:\second"), ("PATHEXT", ".EXE;.CMD"));

        var path = ExecutableLocator.Find("rider", environment, Files(@"C:\first\rider.cmd", @"C:\first\rider.exe", @"C:\second\rider.exe"), isWindows: true);

        Assert.Equal(@"C:\first\rider.exe", path.Value);
    }

    [Fact]
    public void Windows_uses_default_extensions_when_pathext_is_not_set()
    {
        var path = ExecutableLocator.Find("cursor", Environment(("PATH", @"C:\tools")), Files(@"C:\tools\cursor.cmd"), isWindows: true);

        Assert.Equal(@"C:\tools\cursor.cmd", path.Value);
    }

    [Fact]
    public void Windows_command_with_extension_is_not_extended()
    {
        var environment = Environment(("PATH", @"C:\tools"), ("PATHEXT", ".EXE;.CMD"));

        var path = ExecutableLocator.Find("code.cmd", environment, Files(@"C:\tools\code.cmd", @"C:\tools\code.cmd.exe"), isWindows: true);

        Assert.Equal(@"C:\tools\code.cmd", path.Value);
    }

    [Fact]
    public void Windows_command_with_directory_is_not_searched_in_path()
    {
        var environment = Environment(("PATH", @"C:\tools"), ("PATHEXT", ".EXE;.CMD"));

        var path = ExecutableLocator.Find(@"D:\editors\code", environment, Files(@"C:\tools\code.cmd", @"D:\editors\code.cmd"), isWindows: true);

        Assert.Equal(@"D:\editors\code.cmd", path.Value);
    }

    [Fact]
    public void Unix_command_is_found_in_path_without_extensions()
    {
        var environment = Environment(("PATH", "/usr/local/bin:/usr/bin"), ("PATHEXT", ".EXE"));

        var path = ExecutableLocator.Find("x-terminal-emulator", environment, Files("/usr/bin/x-terminal-emulator"), isWindows: false);

        Assert.Equal("/usr/bin/x-terminal-emulator", path.Value);
    }

    [Fact]
    public void Missing_command_is_not_found()
    {
        var path = ExecutableLocator.Find("x-terminal-emulator", Environment(("PATH", "/usr/bin")), Files(), isWindows: false);

        Assert.True(path.HasNoValue);
    }
}
