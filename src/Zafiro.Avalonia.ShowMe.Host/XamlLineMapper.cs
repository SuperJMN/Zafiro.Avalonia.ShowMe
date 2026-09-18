using System.Xml;
using System.Xml.Linq;

namespace Zafiro.Avalonia.ShowMe.Host;

public record XamlElementLocation(int LineNumber, int LinePosition, string? ElementName, string TagName);

public class XamlLineMapper
{
    private readonly Dictionary<string, XamlElementLocation> byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<XamlElementLocation> allElements = [];

    public static XamlLineMapper Parse(string xamlContent)
    {
        var mapper = new XamlLineMapper();
        if (string.IsNullOrWhiteSpace(xamlContent))
        {
            return mapper;
        }

        try
        {
            using var reader = new StringReader(xamlContent);
            using var xmlReader = XmlReader.Create(reader, new XmlReaderSettings { IgnoreWhitespace = false });
            var doc = XDocument.Load(xmlReader, LoadOptions.SetLineInfo);

            if (doc.Root != null)
            {
                Traverse(doc.Root, mapper);
            }
        }
        catch
        {
            // Fallback silencioso si el XML no es válido en este momento
        }

        return mapper;
    }

    private static void Traverse(XElement element, XamlLineMapper mapper)
    {
        var lineInfo = (IXmlLineInfo)element;
        var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0;
        var pos = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0;

        string? name = null;
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var xNameAttr = element.Attribute(x + "Name") ?? element.Attribute("Name");
        if (xNameAttr != null && !string.IsNullOrWhiteSpace(xNameAttr.Value))
        {
            name = xNameAttr.Value.Trim();
        }

        var loc = new XamlElementLocation(line, pos, name, element.Name.LocalName);
        mapper.allElements.Add(loc);

        if (!string.IsNullOrEmpty(name))
        {
            mapper.byName[name] = loc;
        }

        foreach (var child in element.Elements())
        {
            // Ignorar elementos de propiedades complejas tipo Grid.RowDefinitions o Button.Styles
            if (!child.Name.LocalName.Contains('.'))
            {
                Traverse(child, mapper);
            }
        }
    }

    public XamlElementLocation? FindByName(string? name)
    {
        if (!string.IsNullOrEmpty(name) && byName.TryGetValue(name, out var loc))
        {
            return loc;
        }
        return null;
    }

    public XamlElementLocation? FindByTag(string tagName)
    {
        return allElements.FirstOrDefault(e => string.Equals(e.TagName, tagName, StringComparison.OrdinalIgnoreCase));
    }
}
