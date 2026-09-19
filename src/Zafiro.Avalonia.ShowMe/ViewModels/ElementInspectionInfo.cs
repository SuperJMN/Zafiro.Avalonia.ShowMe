using Avalonia;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed record ElementInspectionInfo(
    string TypeName,
    string? ElementName,
    int LineNumber,
    int LinePosition,
    string? SourceUri,
    Rect Bounds,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> Ancestors,
    IReadOnlyDictionary<string, string> Properties
)
{
    public string DisplayTitle => !string.IsNullOrWhiteSpace(ElementName)
        ? $"<{TypeName} x:Name=\"{ElementName}\">"
        : $"<{TypeName}>";

    public string LocationText => LineNumber > 0
        ? $"Línea {LineNumber}:{LinePosition}"
        : "";

    public string ClassesText => Classes.Count > 0 ? string.Join(" ", Classes) : "";

    public string DimensionsText => $"{Bounds.Width:F0} × {Bounds.Height:F0} px";

    public string HoverBadgeText => !string.IsNullOrWhiteSpace(ElementName)
        ? $"{TypeName} #{ElementName}  {Bounds.Width:F0} × {Bounds.Height:F0} px"
        : $"{TypeName}  {Bounds.Width:F0} × {Bounds.Height:F0} px";
}
