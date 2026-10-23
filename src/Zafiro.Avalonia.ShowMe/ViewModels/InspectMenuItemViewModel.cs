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
        Action<InspectMenuItemViewModel>? onOpenInEditor = null)
    {
        TypeName = typeName;
        ElementName = elementName;
        LineNumber = lineNumber;
        LinePosition = linePosition;
        SourceUri = sourceUri;
        ResolvedFilePath = resolvedFilePath;
        RelativeFilePath = relativeFilePath;
        Bounds = bounds;
        CanOpenInEditor = onOpenInEditor != null;
        SelectCommand = EnhancedCommand.Create(() => onSelect(this));
        ViewXamlCommand = EnhancedCommand.Create(() => onViewXaml?.Invoke(this));
        OpenInEditorCommand = EnhancedCommand.Create(() => onOpenInEditor?.Invoke(this));
    }
}
