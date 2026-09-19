using System;
using System.IO;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;

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

    public string NavigationToolTip => !string.IsNullOrWhiteSpace(RelativeFilePath)
        ? $"Abrir {RelativeFilePath}:{LineNumber} en el editor"
        : $"Abrir línea {LineNumber}:{LinePosition} en el editor";

    public ICommand NavigateCommand { get; }

    public InspectMenuItemViewModel(
        string typeName,
        string? elementName,
        int lineNumber,
        int linePosition,
        string? sourceUri,
        string resolvedFilePath,
        string? relativeFilePath,
        Rect bounds,
        Action<InspectMenuItemViewModel> onSelect)
    {
        TypeName = typeName;
        ElementName = elementName;
        LineNumber = lineNumber;
        LinePosition = linePosition;
        SourceUri = sourceUri;
        ResolvedFilePath = resolvedFilePath;
        RelativeFilePath = relativeFilePath;
        Bounds = bounds;
        NavigateCommand = ReactiveCommand.Create(() => onSelect(this));
    }
}
