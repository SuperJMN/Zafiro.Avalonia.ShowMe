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
    IReadOnlyDictionary<string, string> Properties,
    string? ResolvedFilePath = null
)
{
    public string DisplayTitle => !string.IsNullOrWhiteSpace(ElementName)
        ? $"<{TypeName} x:Name=\"{ElementName}\">"
        : $"<{TypeName}>";

    public string SourceFileName => !string.IsNullOrWhiteSpace(ResolvedFilePath)
        ? Path.GetFileName(ResolvedFilePath)
        : (!string.IsNullOrWhiteSpace(SourceUri) ? Path.GetFileName(SourceUri) : "");

    public string LocationText => LineNumber > 0
        ? (!string.IsNullOrWhiteSpace(SourceFileName) ? $"{SourceFileName}:{LineNumber}" : $"Línea {LineNumber}:{LinePosition}")
        : "";

    public string ViewXamlToolTip => !string.IsNullOrWhiteSpace(SourceFileName)
        ? $"Ver {SourceFileName} (línea {LineNumber}) en ShowMe"
        : "Ver el XAML de este control en ShowMe";

    public string EditorToolTip => !string.IsNullOrWhiteSpace(SourceFileName)
        ? $"Abrir {SourceFileName} (línea {LineNumber}) en el editor de código"
        : "Abrir este control en el editor de código";

    public string ClassesText => Classes.Count > 0 ? string.Join(" ", Classes) : "";

    public string DimensionsText => $"{Bounds.Width:F0} × {Bounds.Height:F0} px";

    public string HoverBadgeText => !string.IsNullOrWhiteSpace(ElementName)
        ? $"{TypeName} #{ElementName}  {Bounds.Width:F0} × {Bounds.Height:F0} px"
        : $"{TypeName}  {Bounds.Width:F0} × {Bounds.Height:F0} px";
}
