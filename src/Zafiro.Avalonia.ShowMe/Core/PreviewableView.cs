using System.Xml;

namespace Zafiro.Avalonia.ShowMe.Core;

/// <summary>
/// Tells whether a XAML file can be opened on its own in ShowMe: controls and windows render as such, and styles and
/// resource dictionaries render as the resource catalog. Files missing from disk, files that are not XAML and the
/// application definition cannot.
/// </summary>
public static class PreviewableView
{
    public static bool CanPreview(string file)
    {
        if (!File.Exists(file)) return false;

        try
        {
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            return reader.MoveToContent() == XmlNodeType.Element && reader.LocalName != "Application";
        }
        catch (XmlException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
