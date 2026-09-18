using System.Globalization;
using System.Xml.Linq;

namespace Zafiro.Avalonia.ShowMe.Core;

public static class XamlThemeModifier
{
    public const string ThemeDefault = "Default";
    public const string ThemeLight = "Light";
    public const string ThemeDark = "Dark";

    public static string ApplyTheme(string xamlContent, string? theme)
    {
        return ApplyModifiers(xamlContent, theme, null, null);
    }

    public static string ApplyModifiers(string xamlContent, string? theme, double? customWidth, double? customHeight)
    {
        var hasCustomTheme = !string.IsNullOrWhiteSpace(theme) && !string.Equals(theme, ThemeDefault, StringComparison.OrdinalIgnoreCase);
        var hasCustomDimensions = customWidth.HasValue && customHeight.HasValue && customWidth.Value > 0 && customHeight.Value > 0;

        if (!hasCustomTheme && !hasCustomDimensions)
        {
            return xamlContent;
        }

        try
        {
            var doc = XDocument.Parse(xamlContent, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null)
            {
                return xamlContent;
            }

            var rootName = root.Name.LocalName;

            // 1. Modificar dimensiones si se especifican
            if (hasCustomDimensions)
            {
                XNamespace d = "http://schemas.microsoft.com/expression/blend/2008";
                XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

                // Asegurar que el prefijo xmlns:d esté declarado
                if (root.Attributes().All(a => a.Value != d.NamespaceName))
                {
                    root.Add(new XAttribute(XNamespace.Xmlns + "d", d.NamespaceName));
                }

                // Asegurar que mc:Ignorable incluya "d"
                var mcIgnorableAttr = root.Attribute(mc + "Ignorable");
                if (mcIgnorableAttr == null)
                {
                    if (root.Attributes().All(a => a.Value != mc.NamespaceName))
                    {
                        root.Add(new XAttribute(XNamespace.Xmlns + "mc", mc.NamespaceName));
                    }
                    root.SetAttributeValue(mc + "Ignorable", "d");
                }
                else if (!mcIgnorableAttr.Value.Split(' ').Contains("d"))
                {
                    mcIgnorableAttr.Value = (mcIgnorableAttr.Value + " d").Trim();
                }

                var widthStr = Math.Round(customWidth!.Value).ToString(CultureInfo.InvariantCulture);
                var heightStr = Math.Round(customHeight!.Value).ToString(CultureInfo.InvariantCulture);

                root.SetAttributeValue(d + "DesignWidth", widthStr);
                root.SetAttributeValue(d + "DesignHeight", heightStr);

                // Si es un Window, actualizar también Width y Height directamente
                if (rootName.EndsWith("Window", StringComparison.OrdinalIgnoreCase))
                {
                    root.SetAttributeValue("Width", widthStr);
                    root.SetAttributeValue("Height", heightStr);
                }
            }

            // 2. Modificar tema si se especifica
            if (hasCustomTheme)
            {
                var normalizedTheme = string.Equals(theme, ThemeDark, StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";

                // Si la raíz es Window o deriva de Window, podemos establecer RequestedThemeVariant directamente
                if (rootName.EndsWith("Window", StringComparison.OrdinalIgnoreCase))
                {
                    root.SetAttributeValue("RequestedThemeVariant", normalizedTheme);
                    return doc.ToString(SaveOptions.DisableFormatting);
                }

                // Si es un UserControl u otro Control, envolver el contenido interior en ThemeVariantScope
                var avaloniaNs = root.GetDefaultNamespace();
                if (string.IsNullOrWhiteSpace(avaloniaNs.NamespaceName))
                {
                    avaloniaNs = XNamespace.Get("https://github.com/avaloniaui");
                }

                var themeScopeName = avaloniaNs + "ThemeVariantScope";

                // Si el primer elemento hijo no-propiedad ya es ThemeVariantScope, actualizarlo
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
            }

            return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            // Si el XAML no es XML válido todavía (por ejemplo, durante edición), retornar tal cual
            return xamlContent;
        }
    }
}
