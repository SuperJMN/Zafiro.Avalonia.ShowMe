using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

public sealed class ResourceCatalogView : UserControl
{
    private readonly ResourceGroupModel rootGroup;
    private readonly List<ResourceItemModel> allFlatItems;
    private readonly Control? previewWithControl;
    private readonly ContentControl mainContentHost;
    private string filterQuery = "";

    public ResourceGroupModel RootGroup => rootGroup;
    public List<ResourceItemModel> AllFlatItems => allFlatItems;
    public Control? PreviewWithControl => previewWithControl;
    public bool IsShowingPreviewWith { get; private set; }

    public ResourceCatalogView(ResourceGroupModel rootGroup, Control? previewWithControl = null)
    {
        this.rootGroup = rootGroup;
        this.previewWithControl = previewWithControl;
        allFlatItems = ResourceExtractor.GetAllFlatItems(rootGroup);

        Background = new SolidColorBrush(Color.Parse("#0B0F19"));

        mainContentHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        Content = mainContentHost;

        if (previewWithControl != null)
        {
            ShowPreviewWith();
        }
        else
        {
            ShowAllCards(null);
        }
    }

    public void AttachStyles(Styles styles)
    {
        mainContentHost.Styles.Add(styles);
    }

    public void AttachResources(IResourceDictionary dict)
    {
        foreach (var kvp in dict)
        {
            mainContentHost.Resources[kvp.Key] = kvp.Value;
        }

        if (dict.ThemeDictionaries != null)
        {
            foreach (var themeKvp in dict.ThemeDictionaries)
            {
                if (mainContentHost.Resources.ThemeDictionaries.TryGetValue(themeKvp.Key, out var existing) &&
                    existing is IResourceDictionary existingDict &&
                    themeKvp.Value is IResourceDictionary sourceDict)
                {
                    foreach (var entry in sourceDict)
                    {
                        existingDict[entry.Key] = entry.Value;
                    }
                }
                else
                {
                    mainContentHost.Resources.ThemeDictionaries[themeKvp.Key] = themeKvp.Value;
                }
            }
        }
    }

    public void ShowSelection(string? itemId, string viewMode, string? filterQuery = null)
    {
        this.filterQuery = filterQuery?.Trim().ToLowerInvariant() ?? "";

        if (string.Equals(viewMode, "PreviewWith", StringComparison.OrdinalIgnoreCase))
        {
            ShowPreviewWith();
        }
        else if (string.Equals(viewMode, "Single", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(itemId))
        {
            var item = allFlatItems.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                ShowSingleItem(item);
            }
            else
            {
                ShowAllCards(this.filterQuery);
            }
        }
        else if (string.Equals(viewMode, "Group", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(itemId))
        {
            ShowGroup(itemId);
        }
        else
        {
            ShowAllCards(this.filterQuery);
        }
    }

    public void ShowPreviewWith()
    {
        if (previewWithControl == null)
        {
            ShowAllCards(filterQuery);
            return;
        }

        IsShowingPreviewWith = true;

        var container = new Border
        {
            Padding = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = previewWithControl
        };

        mainContentHost.Content = container;
    }

    public void ShowSingleItem(ResourceItemModel item)
    {
        IsShowingPreviewWith = false;

        var card = CreateResourceCard(item);
        card.Width = 480;
        card.MinHeight = 160;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var outer = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0B0F19")),
            Padding = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = card
        };

        mainContentHost.Content = outer;
    }

    public void ShowGroup(string groupTitle)
    {
        IsShowingPreviewWith = false;

        var matchingItems = allFlatItems
            .Where(i => !string.IsNullOrEmpty(i.OriginPath) && i.OriginPath.Contains(groupTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingItems.Count == 0)
        {
            matchingItems = allFlatItems;
        }

        mainContentHost.Content = BuildCardGridFromItems(matchingItems, groupTitle);
    }

    public void ShowAllCards(string? query = null)
    {
        IsShowingPreviewWith = false;

        if (query != null)
        {
            filterQuery = query.Trim().ToLowerInvariant();
        }

        mainContentHost.Content = BuildFlatView();
    }

    private Control BuildFlatView()
    {
        var filteredItems = FilterItems(allFlatItems);

        if (filteredItems.Count == 0)
        {
            return CreateEmptyState(string.IsNullOrEmpty(filterQuery)
                ? "No se encontraron recursos ni estilos en este archivo."
                : $"No se encontraron recursos que coincidan con '{filterQuery}'.");
        }

        var rootStack = new StackPanel
        {
            Spacing = 28,
            MaxWidth = 1600,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24, 20)
        };

        // 1. ControlThemes
        var themes = filteredItems.Where(i => i.Kind == ResourceItemKind.ControlTheme).ToList();
        if (themes.Count > 0)
        {
            rootStack.Children.Add(CreateCategorySection("🎛", "Temas de Control (ControlThemes)", themes));
        }

        // 2. Styles
        var styles = filteredItems.Where(i => i.Kind == ResourceItemKind.Style).ToList();
        if (styles.Count > 0)
        {
            rootStack.Children.Add(CreateCategorySection("🪄", "Estilos (Styles)", styles));
        }

        // 3. Brushes & Colors
        var brushes = filteredItems.Where(i => i.Kind == ResourceItemKind.Brush || i.Kind == ResourceItemKind.Color).ToList();
        if (brushes.Count > 0)
        {
            rootStack.Children.Add(CreateCategorySection("🎨", "Pinceles y Colores", brushes));
        }

        // 4. Geometries & Icons
        var geometries = filteredItems.Where(i => i.Kind == ResourceItemKind.Geometry).ToList();
        if (geometries.Count > 0)
        {
            rootStack.Children.Add(CreateCategorySection("📐", "Geometrías e Iconos", geometries));
        }

        // 5. Other Resources
        var others = filteredItems.Where(i => i.Kind == ResourceItemKind.Other || i.Kind == ResourceItemKind.Template).ToList();
        if (others.Count > 0)
        {
            rootStack.Children.Add(CreateCategorySection("📦", "Otros Recursos", others));
        }

        return rootStack;
    }

    private Control BuildCardGridFromItems(List<ResourceItemModel> items, string title)
    {
        var filteredItems = FilterItems(items);

        var rootStack = new StackPanel
        {
            Spacing = 20,
            MaxWidth = 1600,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24, 20)
        };

        rootStack.Children.Add(CreateCategorySection("📁", title, filteredItems));
        return rootStack;
    }

    private Control CreateCategorySection(string icon, string title, List<ResourceItemModel> items)
    {
        var section = new StackPanel
        {
            Spacing = 12
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        header.Children.Add(new TextBlock
        {
            Text = $"{icon} {title}",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        header.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E293B")),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = items.Count.ToString(),
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8"))
            }
        });
        section.Children.Add(header);

        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        foreach (var item in items)
        {
            wrap.Children.Add(CreateResourceCard(item));
        }
        section.Children.Add(wrap);

        return section;
    }

    private Control CreateResourceCard(ResourceItemModel item)
    {
        var card = new Border
        {
            Width = 300,
            MinHeight = 130,
            Background = new SolidColorBrush(Color.Parse("#131C2E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#202F49")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 8, Spread = 0, Color = Color.FromArgb(40, 0, 0, 0), OffsetY = 2 })
        };

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var headerStack = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        topRow.Children.Add(CreateKindBadge(item.Kind));

        if (!string.IsNullOrWhiteSpace(item.OriginPath))
        {
            topRow.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1.5),
                Child = new TextBlock
                {
                    Text = item.OriginPath,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 130
                }
            });
        }

        headerStack.Children.Add(topRow);

        var labelBlock = new TextBlock
        {
            Text = item.Label,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16
        };
        headerStack.Children.Add(labelBlock);

        Grid.SetRow(headerStack, 0);
        contentGrid.Children.Add(headerStack);

        var previewHost = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Child = item.GetPreviewControl()
        };
        Grid.SetRow(previewHost, 1);
        contentGrid.Children.Add(previewHost);

        card.Child = contentGrid;
        return card;
    }

    private static Border CreateKindBadge(ResourceItemKind kind)
    {
        var (text, bgHex, fgHex) = kind switch
        {
            ResourceItemKind.Style => ("STYLE", "#8B5CF6", "#FFFFFF"),
            ResourceItemKind.ControlTheme => ("THEME", "#0EA5E9", "#FFFFFF"),
            ResourceItemKind.Brush => ("BRUSH", "#10B981", "#FFFFFF"),
            ResourceItemKind.Color => ("COLOR", "#F59E0B", "#000000"),
            ResourceItemKind.Geometry => ("GEOMETRY", "#0284C7", "#FFFFFF"),
            _ => ("RESOURCE", "#64748B", "#FFFFFF")
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(bgHex)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse(fgHex)),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private List<ResourceItemModel> FilterItems(IEnumerable<ResourceItemModel> items)
    {
        if (string.IsNullOrWhiteSpace(filterQuery))
        {
            return items.ToList();
        }

        return items.Where(i =>
            i.Label.IndexOf(filterQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
            i.KeyOrSelector.IndexOf(filterQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (!string.IsNullOrEmpty(i.OriginPath) && i.OriginPath.IndexOf(filterQuery, StringComparison.OrdinalIgnoreCase) >= 0)
        ).ToList();
    }

    private static Control CreateEmptyState(string message)
    {
        return new Border
        {
            Padding = new Thickness(40),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                TextAlignment = TextAlignment.Center
            }
        };
    }
}
