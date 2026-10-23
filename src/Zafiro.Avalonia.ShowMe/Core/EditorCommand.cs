using System.Diagnostics;
using System.Text;
using CSharpFunctionalExtensions;

namespace Zafiro.Avalonia.ShowMe.Core;

public sealed record EditorCommand(string FileName, IReadOnlyList<string> Arguments)
{
    public const string FilePlaceholder = "{file}";
    public const string LinePlaceholder = "{line}";
    public const string ColumnPlaceholder = "{column}";
    public const string DefaultTerminal = "x-terminal-emulator";

    private static readonly string[] TerminalEditors = ["vi", "vim", "nvim", "nano", "micro", "hx", "kak"];

    private static readonly Dictionary<string, string> KnownTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "-g {file}:{line}:{column}",
        ["code-insiders"] = "-g {file}:{line}:{column}",
        ["codium"] = "-g {file}:{line}:{column}",
        ["cursor"] = "-g {file}:{line}:{column}",
        ["rider"] = "--line {line} --column {column} {file}",
        ["rider64"] = "--line {line} --column {column} {file}",
        ["idea"] = "--line {line} --column {column} {file}",
        ["idea64"] = "--line {line} --column {column} {file}",
        ["zed"] = "{file}:{line}:{column}",
        ["subl"] = "{file}:{line}:{column}",
        ["hx"] = "{file}:{line}:{column}",
        ["vi"] = "+{line} {file}",
        ["vim"] = "\"+call cursor({line},{column})\" {file}",
        ["nvim"] = "\"+call cursor({line},{column})\" {file}",
        ["gvim"] = "\"+call cursor({line},{column})\" {file}",
        ["nano"] = "+{line},{column} {file}",
        ["micro"] = "+{line}:{column} {file}",
        ["kak"] = "+{line}:{column} {file}",
        ["emacs"] = "+{line}:{column} {file}",
        ["emacsclient"] = "-n +{line}:{column} {file}",
    };

    /// <summary>
    /// Resolves the command that opens <paramref name="file"/> at the given position.
    /// The explicit template (from --editor or the saved setting) wins; otherwise $VISUAL and then $EDITOR are used, with known arguments for common editors.
    /// Terminal editors run inside $TERMINAL or, failing that, x-terminal-emulator; without either they cannot be opened.
    /// None means no editor is configured; the file is never handed to the system handler.
    /// The program is located with <paramref name="findExecutable"/> so scripts such as code.cmd also start on Windows.
    /// </summary>
    public static Result<Maybe<EditorCommand>> Resolve(
        string? template,
        Func<string, string?> environment,
        Func<string, Maybe<string>> findExecutable,
        string file,
        int line,
        int column)
    {
        if (!string.IsNullOrWhiteSpace(template))
        {
            return FromTemplate(template, file, line, column).Map(c => c.Locate(findExecutable));
        }

        var editor = new[] { environment("VISUAL"), environment("EDITOR") }.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
        if (editor == null)
        {
            return Maybe<EditorCommand>.None;
        }

        var name = Path.GetFileNameWithoutExtension(Tokenize(editor).FirstOrDefault() ?? "");
        var arguments = KnownTemplates.TryGetValue(name, out var known) ? known : FilePlaceholder;
        var command = FromTemplate($"{editor} {arguments}", file, line, column);

        if (!TerminalEditors.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return command.Map(c => c.Locate(findExecutable));
        }

        var terminal = environment("TERMINAL");
        if (string.IsNullOrWhiteSpace(terminal))
        {
            terminal = findExecutable(DefaultTerminal).Map(_ => DefaultTerminal).GetValueOrDefault();
        }

        if (terminal == null)
        {
            return Result.Failure<Maybe<EditorCommand>>($"{name} necesita un terminal; define $TERMINAL o configura otro editor en Más acciones → Editor de código…");
        }

        return command.Map(c => InTerminal(c, terminal).Locate(findExecutable));
    }

    public static Maybe<EditorCommand> FromTemplate(string template, string file, int line, int column)
    {
        var tokens = Tokenize(template);
        if (tokens.Count == 0)
        {
            return Maybe<EditorCommand>.None;
        }

        if (!tokens.Skip(1).Any(t => t.Contains(FilePlaceholder, StringComparison.Ordinal)))
        {
            tokens.Add(FilePlaceholder);
        }

        var arguments = tokens.Skip(1)
            .Select(t => t
                .Replace(FilePlaceholder, file, StringComparison.Ordinal)
                .Replace(LinePlaceholder, line.ToString(), StringComparison.Ordinal)
                .Replace(ColumnPlaceholder, column.ToString(), StringComparison.Ordinal))
            .ToList();

        return new EditorCommand(tokens[0], arguments);
    }

    public ProcessStartInfo ToStartInfo()
    {
        var startInfo = new ProcessStartInfo(FileName) { UseShellExecute = false };
        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private EditorCommand Locate(Func<string, Maybe<string>> findExecutable)
    {
        return this with { FileName = findExecutable(FileName).GetValueOrDefault(FileName) };
    }

    private static EditorCommand InTerminal(EditorCommand command, string terminal)
    {
        var tokens = Tokenize(terminal);
        return new EditorCommand(tokens[0], [..tokens.Skip(1), "-e", command.FileName, ..command.Arguments]);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var hasToken = false;
        char? quote = null;

        foreach (var c in text)
        {
            if (quote != null)
            {
                if (c == quote)
                {
                    quote = null;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }

        if (hasToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
