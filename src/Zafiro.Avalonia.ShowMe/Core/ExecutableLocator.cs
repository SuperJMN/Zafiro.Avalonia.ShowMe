using CSharpFunctionalExtensions;

namespace Zafiro.Avalonia.ShowMe.Core;

public static class ExecutableLocator
{
    private const string DefaultPathExtensions = ".COM;.EXE;.BAT;.CMD";

    /// <summary>
    /// Finds <paramref name="name"/> in the current PATH the way the current platform's shell does.
    /// </summary>
    public static Maybe<string> Find(string name)
    {
        return Find(name, Environment.GetEnvironmentVariable, IsExecutable, OperatingSystem.IsWindows());
    }

    /// <summary>
    /// Finds <paramref name="name"/> in the PATH. On Windows, a name without extension is tried with every PATHEXT extension,
    /// so commands installed as .cmd or .bat scripts (code, cursor, rider...) resolve to the script itself.
    /// A name with a directory is not searched in the PATH.
    /// </summary>
    public static Maybe<string> Find(string name, Func<string, string?> environment, Func<string, bool> fileExists, bool isWindows)
    {
        char[] directorySeparators = isWindows ? ['\\', '/'] : ['/'];
        var directorySeparator = isWindows ? '\\' : '/';
        var fileName = name[(name.LastIndexOfAny(directorySeparators) + 1)..];

        string[] extensions = isWindows && !fileName.Contains('.')
            ? (environment("PATHEXT") ?? DefaultPathExtensions)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(extension => extension.ToLowerInvariant())
                .ToArray()
            : [""];

        string[] directories = name.IndexOfAny(directorySeparators) >= 0
            ? [""]
            : (environment("PATH") ?? "")
                .Split(isWindows ? ';' : ':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => directory.Trim('"').TrimEnd(directorySeparators) + directorySeparator)
                .ToArray();

        return directories
            .SelectMany(directory => extensions.Select(extension => directory + name + extension))
            .TryFirst(fileExists);
    }

    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        return (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }
}
