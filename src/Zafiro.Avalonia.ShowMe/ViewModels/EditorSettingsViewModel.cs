using CSharpFunctionalExtensions;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Core;
using Zafiro.UI.Commands;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

/// <summary>
/// The "Editor de código…" dialog: edits the saved editor command.
/// </summary>
public sealed class EditorSettingsViewModel : ReactiveObject
{
    private readonly CodeEditor codeEditor;
    private string command;
    private string? error;

    public EditorSettingsViewModel(CodeEditor codeEditor, Action close)
    {
        this.codeEditor = codeEditor;
        command = codeEditor.SavedTemplate ?? "";

        SaveCommand = EnhancedCommand.Create(() =>
        {
            var saved = codeEditor.Save(Command);
            Error = saved.IsFailure ? saved.Error : null;
            if (saved.IsSuccess)
            {
                close();
            }
        });
        CancelCommand = EnhancedCommand.Create(close);
    }

    /// <summary>
    /// The editor command, with the {file}, {line} and {column} placeholders. Blank removes the saved editor.
    /// </summary>
    public string Command
    {
        get => command;
        set => this.RaiseAndSetIfChanged(ref command, value);
    }

    public string? Error
    {
        get => error;
        private set => this.RaiseAndSetIfChanged(ref error, value);
    }

    public string CurrentEditorText => codeEditor.Source.Match(
        source => $"Editor actual ({source.Origin}): {source.Command}",
        () => "Ahora no hay ningún editor configurado.");

    public bool IsOverriddenByCommandLine => codeEditor.CommandLineTemplate != null;

    public IEnhancedCommand SaveCommand { get; }
    public IEnhancedCommand CancelCommand { get; }
}
