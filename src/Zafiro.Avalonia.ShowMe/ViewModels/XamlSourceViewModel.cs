namespace Zafiro.Avalonia.ShowMe.ViewModels;

/// <summary>
/// The XAML file shown in ShowMe's read-only viewer, with the line of the element to highlight.
/// </summary>
public sealed record XamlSourceViewModel(string FilePath, string Text, int LineNumber, int LinePosition)
{
    public string FileName => Path.GetFileName(FilePath);

    public string LocationText => LineNumber > 0 ? $"{FileName}:{LineNumber}" : FileName;
}
