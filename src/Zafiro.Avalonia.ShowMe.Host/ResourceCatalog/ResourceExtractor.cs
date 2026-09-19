using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

public static class ResourceExtractor
{
    public static ResourceGroupModel Extract(object loaded, Assembly? targetAssembly, string rootName = "Archivo raíz")
    {
        var rootGroup = new ResourceGroupModel
        {
            Title = rootName,
            Icon = loaded is Styles ? "🪄" : "📦"
        };

        if (loaded is Styles styles)
        {
            ExtractFromStyles(styles, rootGroup, targetAssembly, parentStyles: styles, originPath: null);
        }
        else if (loaded is IResourceDictionary dict)
        {
            ExtractFromResourceDictionary(dict, rootGroup, targetAssembly, originPath: null);
        }
        else if (loaded is Style style)
        {
            ExtractStyleItem(style, rootGroup, targetAssembly, parentStyles: null, originPath: null);
        }
        else if (loaded is ControlTheme theme)
        {
            ExtractThemeItem("Default", theme, rootGroup, targetAssembly, originPath: null);
        }

        return rootGroup;
    }

    public static List<ResourceItemModel> GetAllFlatItems(ResourceGroupModel root)
    {
        var result = new List<ResourceItemModel>();
        CollectFlatItems(root, result);
        return result;
    }

    private static void CollectFlatItems(ResourceGroupModel group, List<ResourceItemModel> list)
    {
        list.AddRange(group.Items);
        foreach (var sub in group.Subgroups)
        {
            CollectFlatItems(sub, list);
        }
    }

    private static void ExtractFromStyles(Styles styles, ResourceGroupModel group, Assembly? targetAssembly, Styles? parentStyles, string? originPath)
    {
        // 1. Child styles and control themes
        foreach (var child in styles)
        {
            if (child is Style style)
            {
                ExtractStyleItem(style, group, targetAssembly, parentStyles, originPath);
            }
            else if (child is ControlTheme theme)
            {
                var key = theme.TargetType?.Name ?? "ControlTheme";
                ExtractThemeItem(key, theme, group, targetAssembly, originPath);
            }
            else if (child is StyleInclude inc)
            {
                var incSource = inc.Source?.ToString() ?? "StyleInclude";
                var subGroup = new ResourceGroupModel
                {
                    Title = $"Include: {System.IO.Path.GetFileName(incSource)}",
                    Icon = "🔗",
                    OriginPath = incSource
                };
                if (inc.Loaded is Styles incStyles)
                {
                    ExtractFromStyles(incStyles, subGroup, targetAssembly, incStyles, originPath: incSource);
                }
                if (subGroup.TotalItemCount > 0)
                {
                    group.Subgroups.Add(subGroup);
                }
            }
            else if (child is Styles nestedStyles)
            {
                var subGroup = new ResourceGroupModel
                {
                    Title = "Styles (Anidados)",
                    Icon = "🪄",
                    OriginPath = originPath
                };
                ExtractFromStyles(nestedStyles, subGroup, targetAssembly, parentStyles ?? nestedStyles, originPath);
                if (subGroup.TotalItemCount > 0)
                {
                    group.Subgroups.Add(subGroup);
                }
            }
        }

        // 2. Resources inside <Styles.Resources>
        if (styles.Resources is IResourceDictionary resDict && resDict.Count > 0)
        {
            var resGroup = new ResourceGroupModel
            {
                Title = "Recursos (Styles.Resources)",
                Icon = "📦",
                OriginPath = originPath
            };
            ExtractFromResourceDictionary(resDict, resGroup, targetAssembly, originPath: "Styles.Resources");
            if (resGroup.TotalItemCount > 0)
            {
                group.Subgroups.Add(resGroup);
            }
        }
    }

    private static void ExtractFromResourceDictionary(IResourceDictionary dict, ResourceGroupModel group, Assembly? targetAssembly, string? originPath)
    {
        // 1. Direct entries
        foreach (var entry in dict)
        {
            ProcessDictionaryEntry(entry.Key, entry.Value, group, targetAssembly, originPath);
        }

        // 2. ThemeDictionaries (Dark, Light, Default)
        if (dict.ThemeDictionaries != null && dict.ThemeDictionaries.Count > 0)
        {
            var themesParentGroup = new ResourceGroupModel
            {
                Title = "Diccionarios de Tema (ThemeDictionaries)",
                Icon = "🌓",
                OriginPath = originPath
            };

            foreach (var kvp in dict.ThemeDictionaries)
            {
                var variantKey = kvp.Key?.Key?.ToString() ?? kvp.Key?.ToString() ?? "Theme";
                var themeGroup = new ResourceGroupModel
                {
                    Title = $"Tema: {variantKey}",
                    Icon = "🎨",
                    OriginPath = $"ThemeDictionaries: {variantKey}"
                };

                if (kvp.Value is IResourceDictionary themeDict)
                {
                    ExtractFromResourceDictionary(themeDict, themeGroup, targetAssembly, originPath: $"Theme: {variantKey}");
                }

                if (themeGroup.TotalItemCount > 0)
                {
                    themesParentGroup.Subgroups.Add(themeGroup);
                }
            }

            if (themesParentGroup.TotalItemCount > 0)
            {
                group.Subgroups.Add(themesParentGroup);
            }
        }

        // 3. MergedDictionaries
        if (dict.MergedDictionaries != null && dict.MergedDictionaries.Count > 0)
        {
            var mergedParentGroup = new ResourceGroupModel
            {
                Title = "Diccionarios Combinados (MergedDictionaries)",
                Icon = "📚",
                OriginPath = originPath
            };

            for (int i = 0; i < dict.MergedDictionaries.Count; i++)
            {
                var merged = dict.MergedDictionaries[i];
                var name = merged is ResourceInclude rinc
                    ? System.IO.Path.GetFileName(rinc.Source?.ToString() ?? $"Include {i + 1}")
                    : $"Merged {i + 1}";

                var subGroup = new ResourceGroupModel
                {
                    Title = name,
                    Icon = "📄",
                    OriginPath = name
                };

                if (merged is IResourceDictionary mergedDict)
                {
                    ExtractFromResourceDictionary(mergedDict, subGroup, targetAssembly, originPath: name);
                }

                if (subGroup.TotalItemCount > 0)
                {
                    mergedParentGroup.Subgroups.Add(subGroup);
                }
            }

            if (mergedParentGroup.TotalItemCount > 0)
            {
                group.Subgroups.Add(mergedParentGroup);
            }
        }
    }

    private static void ProcessDictionaryEntry(object? keyObj, object? value, ResourceGroupModel group, Assembly? targetAssembly, string? originPath)
    {
        if (keyObj == null) return;

        var keyStr = keyObj is Type t ? t.Name : keyObj.ToString() ?? "";

        if (value is ControlTheme theme)
        {
            ExtractThemeItem(keyStr, theme, group, targetAssembly, originPath);
        }
        else if (value is Style style)
        {
            ExtractStyleItem(style, group, targetAssembly, parentStyles: null, originPath);
        }
        else if (value is IBrush brush)
        {
            group.Items.Add(new ResourceItemModel
            {
                Kind = ResourceItemKind.Brush,
                Label = $"Brush: {keyStr}",
                KeyOrSelector = keyStr,
                OriginPath = originPath,
                RawValue = brush,
                PreviewControlFactory = () => PreviewControlFactory.CreatePreviewForBrush(brush, keyStr)
            });
        }
        else if (value is Color color)
        {
            group.Items.Add(new ResourceItemModel
            {
                Kind = ResourceItemKind.Color,
                Label = $"Color: {keyStr}",
                KeyOrSelector = keyStr,
                OriginPath = originPath,
                RawValue = color,
                PreviewControlFactory = () => PreviewControlFactory.CreatePreviewForColor(color, keyStr)
            });
        }
        else
        {
            group.Items.Add(new ResourceItemModel
            {
                Kind = ResourceItemKind.Other,
                Label = $"Recurso: {keyStr}",
                KeyOrSelector = keyStr,
                OriginPath = originPath,
                RawValue = value,
                PreviewControlFactory = () => PreviewControlFactory.CreatePreviewForOther(value, keyStr)
            });
        }
    }

    private static void ExtractStyleItem(Style style, ResourceGroupModel group, Assembly? targetAssembly, Styles? parentStyles, string? originPath)
    {
        var selectorStr = style.Selector?.ToString() ?? "Style";
        group.Items.Add(new ResourceItemModel
        {
            Kind = ResourceItemKind.Style,
            Label = $"Style: {selectorStr}",
            KeyOrSelector = selectorStr,
            OriginPath = originPath,
            RawValue = style,
            PreviewControlFactory = () => PreviewControlFactory.CreatePreviewForStyle(style, targetAssembly, parentStyles)
        });
    }

    private static void ExtractThemeItem(string key, ControlTheme theme, ResourceGroupModel group, Assembly? targetAssembly, string? originPath)
    {
        var targetTypeName = theme.TargetType?.Name ?? key;
        var displayLabel = string.Equals(key, targetTypeName, StringComparison.OrdinalIgnoreCase)
            ? $"ControlTheme: {key}"
            : $"ControlTheme: {targetTypeName} ({key})";

        group.Items.Add(new ResourceItemModel
        {
            Kind = ResourceItemKind.ControlTheme,
            Label = displayLabel,
            KeyOrSelector = key,
            TargetTypeName = targetTypeName,
            OriginPath = originPath,
            RawValue = theme,
            PreviewControlFactory = () => PreviewControlFactory.CreatePreviewForTheme(theme, targetAssembly)
        });
    }
}
