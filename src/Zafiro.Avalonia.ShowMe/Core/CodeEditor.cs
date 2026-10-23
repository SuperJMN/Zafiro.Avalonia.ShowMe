using System.Diagnostics;
using CSharpFunctionalExtensions;
using ReactiveUI;

namespace Zafiro.Avalonia.ShowMe.Core;

/// <summary>
/// The external code editor the user can open an element in. It is optional: viewing the XAML happens inside ShowMe.
/// The command comes from --editor, then from the saved setting, then from $VISUAL or $EDITOR.
/// ShowMe never falls back to the system handler, because .axaml files may be associated with ShowMe itself.
/// </summary>
public sealed class CodeEditor : ReactiveObject
{
    private readonly string? commandLineTemplate;
    private readonly IUserSettingsStore store;
    private readonly Func<string, string?> environment;
    private readonly Func<string, Maybe<string>> findExecutable;
    private readonly Action<ProcessStartInfo> start;
    private string? savedTemplate;
    private bool isAvailable;

    public CodeEditor(
        string? commandLineTemplate,
        IUserSettingsStore store,
        Func<string, string?> environment,
        Func<string, Maybe<string>> findExecutable,
        Action<ProcessStartInfo> start)
    {
        this.commandLineTemplate = Normalize(commandLineTemplate);
        this.store = store;
        this.environment = environment;
        this.findExecutable = findExecutable;
        this.start = start;
        savedTemplate = Normalize(store.Load().Editor);
        isAvailable = Source.HasValue;
    }

    public static CodeEditor ForCurrentUser(string? commandLineTemplate) => new(
        commandLineTemplate,
        new JsonUserSettingsStore(JsonUserSettingsStore.DefaultPath),
        Environment.GetEnvironmentVariable,
        ExecutableLocator.Find,
        startInfo => Process.Start(startInfo)?.Dispose());

    public string? CommandLineTemplate => commandLineTemplate;

    public string? SavedTemplate
    {
        get => savedTemplate;
        private set => this.RaiseAndSetIfChanged(ref savedTemplate, value);
    }

    /// <summary>
    /// True when some source gives an editor command, so "Abrir en el editor" can be offered.
    /// </summary>
    public bool IsAvailable
    {
        get => isAvailable;
        private set => this.RaiseAndSetIfChanged(ref isAvailable, value);
    }

    /// <summary>
    /// The command in effect and where it comes from, or None.
    /// </summary>
    public Maybe<(string Origin, string Command)> Source
    {
        get
        {
            if (commandLineTemplate != null) return ("--editor", commandLineTemplate);
            if (savedTemplate != null) return ("configuración guardada", savedTemplate);

            var visual = Normalize(environment("VISUAL"));
            if (visual != null) return ("$VISUAL", visual);

            var editor = Normalize(environment("EDITOR"));
            if (editor != null) return ("$EDITOR", editor);

            return Maybe<(string, string)>.None;
        }
    }

    /// <summary>
    /// Saves the command (blank removes it) so it applies on every later run.
    /// </summary>
    public Result Save(string? template)
    {
        var normalized = Normalize(template);
        return store.Save(store.Load() with { Editor = normalized })
            .Tap(() =>
            {
                SavedTemplate = normalized;
                IsAvailable = Source.HasValue;
                this.RaisePropertyChanged(nameof(Source));
            });
    }

    /// <summary>
    /// Opens <paramref name="file"/> at the given position and returns the name of the program that was started.
    /// </summary>
    public Result<string> Open(string file, int line, int column)
    {
        return EditorCommand.Resolve(commandLineTemplate ?? savedTemplate, environment, findExecutable, file, line, column)
            .Bind(command => command.ToResult("no hay ningún editor de código configurado (Más acciones → Editor de código…)"))
            .Bind(command => Result.Try(
                () =>
                {
                    start(command.ToStartInfo());
                    return Path.GetFileName(command.FileName);
                },
                e => $"no se pudo iniciar {Path.GetFileName(command.FileName)}: {e.Message}"));
    }

    private static string? Normalize(string? template) => string.IsNullOrWhiteSpace(template) ? null : template.Trim();
}
