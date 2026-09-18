using System.Xml.Linq;

namespace Zafiro.Avalonia.ShowMe.Core;

public static class XamlThemeModifier
{
    public const string ThemeDefault = "Default";
    public const string ThemeLight = "Light";
    public const string ThemeDark = "Dark";

    public static string ApplyTheme(string xamlContent, string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme) || string.Equals(theme, ThemeDefault, StringComparison.OrdinalIgnoreCase))
        {
            return xamlContent;
        }

        var normalizedTheme = string.Equals(theme, ThemeDark, StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";

        try
        {
            var doc = XDocument.Parse(xamlContent, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null)
            {
                return xamlContent;
            }

            var rootName = root.Name.LocalName;

            // Si la raíz es Window o deriva de Window, podemos establecer RequestedThemeVariant directamente
            if (rootName.EndsWith("Window", StringComparison.OrdinalIgnoreCase))
            {
                root.SetAttributeValue("RequestedThemeVariant", normalizedTheme);
                return doc.ToString(SaveOptions.DisableFormatting);
            }

            // Si es un UserControl u otro Control, envolver el contenido interior en ThemeVariantScope
            // o verificar si ya tiene ThemeVariantScope como hijo raíz
            var avaloniaNs = root.GetDefaultNamespace();
            if (string.IsNullOrWhiteSpace(avaloniaNs.NamespaceName))
            {
                avaloniaNs = XNamespace.Get("https://github.com/avaloniaui");
            }

            var themeScopeName = avaloniaNs + "ThemeVariantScope";

            // Si el único elemento hijo es ThemeVariantScope, actualizarlo
            var firstChildElement = root.Elements().FirstOrDefault(e => !e.Name.LocalName.Contains('.'));
            if (firstChildElement != null && firstChildElement.Name.LocalName == "ThemeVariantScope")
            {
                firstChildElement.SetAttributeValue("RequestedThemeVariant", normalizedTheme);
                return doc.ToString(SaveOptions.DisableFormatting);
            }

            // Si no, envolver los elementos de contenido (excluyendo propiedades adjuntas como UserControl.Resources)
            var contentElements = root.Elements().Where(e => !e.Name.LocalName.Contains('.')).ToList();
            if (contentElements.Count > 0)
            {
                var scopeElement = new XElement(themeScopeName,
                    new XAttribute("RequestedThemeVariant", normalizedTheme),
                    contentElements);

                foreach (var elem in contentElements)
                {
                    elem.Remove();
                }

                root.Add(scopeElement);
                return doc.ToString(SaveOptions.DisableFormatting);
            }

            // Fallback: intentar establecer RequestedThemeVariant en la raíz
            root.SetAttributeValue("RequestedThemeVariant", normalizedTheme);
            return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            // Si el XAML no es XML válido todavía (por ejemplo, durante edición), retornar tal cual
            return xamlContent;
        }
    }
}
