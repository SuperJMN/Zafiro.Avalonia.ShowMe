using System;
using System.IO;
using Avalonia;
using ReactiveUI;
using Zafiro.UI.Commands;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed class InspectMenuItemViewModel : ReactiveObject
{
    public string TypeName { get; }
    public string? ElementName { get; }
    public int LineNumber { get; }
    public int LinePosition { get; }
    public string? SourceUri { get; }
    public string ResolvedFilePath { get; }
    public string? RelativeFilePath { get; } // null if local file!
    public Rect Bounds { get; }

    /// <summary>
    /// Where the element sits in the visual tree of the preview, which tells apart the instances of a template.
    /// </summary>
    public string? TreePath { get; }

    public string LocationText => LineNumber > 0 ? $"{LineNumber}:{LinePosition}" : "";

    public string DisplayText
    {
        get
        {
            var namePart = !string.IsNullOrWhiteSpace(ElementName) ? $" #{ElementName}" : "";
            var filePart = !string.IsNullOrWhiteSpace(RelativeFilePath) ? $" ({RelativeFilePath})" : "";
            var locPart = !string.IsNullOrWhiteSpace(LocationText) ? $" [{LocationText}]" : "";
            return $"{TypeName}{namePart}{filePart}{locPart}";
        }
    }

    public IEnhancedCommand SelectCommand { get; }

    /// <summary>
    /// Selects the element and shows its XAML inside ShowMe.
    /// </summary>
    public IEnhancedCommand ViewXamlCommand { get; }

    /// <summary>
    /// Selects the element and opens it in the external editor. Only offered when <see cref="CanOpenInEditor"/>.
    /// </summary>
    public IEnhancedCommand OpenInEditorCommand { get; }

    public bool CanOpenInEditor { get; }

    /// <summary>
    /// Selects the element and opens the view that defines it in ShowMe. Only offered when <see cref="CanOpenView"/>.
    /// </summary>
    public IEnhancedCommand OpenViewCommand { get; }

    /// <summary>
    /// True when the element comes from another file that ShowMe can preview.
    /// </summary>
    public bool CanOpenView { get; }

    public InspectMenuItemViewModel(
        string typeName,
        string? elementName,
        int lineNumber,
        int linePosition,
        string? sourceUri,
        string resolvedFilePath,
        string? relativeFilePath,
        Rect bounds,
        Action<InspectMenuItemViewModel> onSelect,
        Action<InspectMenuItemViewModel>? onViewXaml = null,
        Action<InspectMenuItemViewModel>? onOpenInEditor = null,
        Func<InspectMenuItemViewModel, Task>? onOpenView = null,
        string? treePath = null)
    {
        TypeName = typeName;
        ElementName = elementName;
        LineNumber = lineNumber;
        LinePosition = linePosition;
        SourceUri = sourceUri;
        ResolvedFilePath = resolvedFilePath;
        RelativeFilePath = relativeFilePath;
        Bounds = bounds;
        TreePath = treePath;
        CanOpenInEditor = onOpenInEditor != null;
        SelectCommand = EnhancedCommand.Create(() => onSelect(this));
        ViewXamlCommand = EnhancedCommand.Create(() => onViewXaml?.Invoke(this));
        OpenInEditorCommand = EnhancedCommand.Create(() => onOpenInEditor?.Invoke(this));
        CanOpenView = onOpenView != null;
        OpenViewCommand = EnhancedCommand.Create(() => onOpenView?.Invoke(this) ?? Task.CompletedTask);
    }
}
