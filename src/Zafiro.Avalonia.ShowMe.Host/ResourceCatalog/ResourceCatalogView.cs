using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

public sealed class ResourceCatalogView : UserControl
{
    private enum CatalogViewMode { Flat, Tree, PreviewWith }

    private readonly ResourceGroupModel rootGroup;
    private readonly List<ResourceItemModel> allFlatItems;
    private readonly Control? previewWithControl;
    private string filterQuery = "";
    private CatalogViewMode currentMode = CatalogViewMode.Flat;

    private readonly ContentControl mainContentHost;
    private readonly Button? previewWithButton;
    private readonly Button flatViewButton;
    private readonly Button treeViewButton;
    private readonly TextBox filterTextBox;
    private readonly TextBlock statsTextBlock;

    private TextBlock? previewWithText;
    private TextBlock flatViewText = null!;
    private TextBlock treeViewText = null!;

    // View caches
    private Control? flatViewControl;
    private Control? treeViewControl;
    private StackPanel? treeDetailPanel;
    private ResourceGroupModel? selectedGroup;

    public ResourceCatalogView(ResourceGroupModel rootGroup, Control? previewWithControl = null)
    {
        this.rootGroup = rootGroup;
        this.previewWithControl = previewWithControl;
        allFlatItems = ResourceExtractor.GetAllFlatItems(rootGroup);

        Background = new SolidColorBrush(Color.Parse("#0B0F19"));

        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        // Header toolbar
        var header = CreateHeaderToolbar(out previewWithButton, out flatViewButton, out treeViewButton, out filterTextBox, out statsTextBlock);
        Grid.SetRow(header, 0);
        mainGrid.Children.Add(header);

        // Main content host
        mainContentHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(mainContentHost, 1);
        mainGrid.Children.Add(mainContentHost);

        Content = mainGrid;

        UpdateStatsText();

        if (previewWithControl != null)
        {
            SwitchToPreviewWithView();
        }
        else
        {
            SwitchToFlatView();
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

    private Control CreateHeaderToolbar(out Button? btnPreview, out Button btnFlat, out Button btnTree, out TextBox tbFilter, out TextBlock tbStats)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#111827")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1F2937")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto")
        };

        // Left: Title & Stats
        var titleStack = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(new TextBlock
        {
            Text = "🎨",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = "Catálogo de Recursos y Estilos",
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        titleStack.Children.Add(titleRow);

        tbStats = new TextBlock
        {
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.Parse("#9CA3AF")),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(tbStats);
        Grid.SetColumn(titleStack, 0);
        grid.Children.Add(titleStack);

        // Middle: Search / Filter
        var filterBox = new TextBox
        {
            PlaceholderText = "Filtrar por nombre, selector o clave...",
            Width = 260,
            Height = 32,
            Margin = new Thickness(24, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        filterBox.TextChanged += (_, _) =>
        {
            filterQuery = filterBox.Text?.Trim().ToLowerInvariant() ?? "";
            RefreshCurrentView();
        };
        tbFilter = filterBox;
        Grid.SetColumn(tbFilter, 1);
        grid.Children.Add(tbFilter);

        // Right: View Mode Toggle Buttons
        var toggleBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (previewWithControl != null)
        {
            previewWithText = new TextBlock
            {
                Text = "🖼️ Vista previa",
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPreview = new Button
            {
                Content = previewWithText,
                Height = 32,
                Padding = new Thickness(12, 0),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center
            };
            btnPreview.Click += (_, _) => SwitchToPreviewWithView();
            toggleBar.Children.Add(btnPreview);
        }
        else
        {
            btnPreview = null;
            previewWithText = null;
        }

        flatViewText = new TextBlock
        {
            Text = "≡ Lista plana",
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        btnFlat = new Button
        {
            Content = flatViewText,
            Height = 32,
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center
        };
        btnFlat.Click += (_, _) => SwitchToFlatView();
        toggleBar.Children.Add(btnFlat);

        treeViewText = new TextBlock
        {
            Text = "⑂ Lista de árbol",
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        btnTree = new Button
        {
            Content = treeViewText,
            Height = 32,
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center
        };
        btnTree.Click += (_, _) => SwitchToTreeView();
        toggleBar.Children.Add(btnTree);

        Grid.SetColumn(toggleBar, 2);
        grid.Children.Add(toggleBar);

        border.Child = grid;
        return border;
    }

    private void UpdateStatsText()
    {
        int stylesCount = allFlatItems.Count(i => i.Kind == ResourceItemKind.Style);
        int themesCount = allFlatItems.Count(i => i.Kind == ResourceItemKind.ControlTheme);
        int brushesCount = allFlatItems.Count(i => i.Kind == ResourceItemKind.Brush || i.Kind == ResourceItemKind.Color);
        int otherCount = allFlatItems.Count(i => i.Kind == ResourceItemKind.Other);

        var parts = new List<string>();
        if (stylesCount > 0) parts.Add($"{stylesCount} estilo{(stylesCount > 1 ? "s" : "")}");
        if (themesCount > 0) parts.Add($"{themesCount} tema{(themesCount > 1 ? "s" : "")} de control");
        if (brushesCount > 0) parts.Add($"{brushesCount} pincel{(brushesCount > 1 ? "es" : "")}/color{(brushesCount > 1 ? "es" : "")}");
        if (otherCount > 0) parts.Add($"{otherCount} otro{(otherCount > 1 ? "s" : "")} recurso{(otherCount > 1 ? "s" : "")}");

        statsTextBlock.Text = parts.Count > 0 ? string.Join(" · ", parts) : "0 recursos encontrados";
    }

    public void SwitchToPreviewWithView()
    {
        if (previewWithControl == null) return;
        currentMode = CatalogViewMode.PreviewWith;
        UpdateButtonStyles();

        var container = new Border
        {
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = previewWithControl
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = container
        };

        mainContentHost.Content = scroll;
    }

    public void SwitchToFlatView()
    {
        currentMode = CatalogViewMode.Flat;
        UpdateButtonStyles();
        flatViewControl = BuildFlatView();
        mainContentHost.Content = flatViewControl;
    }

    public void SwitchToTreeView()
    {
        currentMode = CatalogViewMode.Tree;
        UpdateButtonStyles();
        treeViewControl = BuildTreeView();
        mainContentHost.Content = treeViewControl;
    }

    private void UpdateButtonStyles()
    {
        var activeBg = new SolidColorBrush(Color.Parse("#38BDF8"));
        var activeFg = Brushes.Black;
        var inactiveBg = new SolidColorBrush(Color.Parse("#1F2937"));
        var inactiveFg = new SolidColorBrush(Color.Parse("#E2E8F0"));

        if (previewWithButton != null && previewWithText != null)
        {
            var isAct = currentMode == CatalogViewMode.PreviewWith;
            previewWithButton.Background = isAct ? activeBg : inactiveBg;
            previewWithButton.Foreground = isAct ? activeFg : inactiveFg;
            previewWithText.Foreground = isAct ? activeFg : inactiveFg;
        }

        var isFlat = currentMode == CatalogViewMode.Flat;
        flatViewButton.Background = isFlat ? activeBg : inactiveBg;
        flatViewButton.Foreground = isFlat ? activeFg : inactiveFg;
        flatViewText.Foreground = isFlat ? activeFg : inactiveFg;

        var isTree = currentMode == CatalogViewMode.Tree;
        treeViewButton.Background = isTree ? activeBg : inactiveBg;
        treeViewButton.Foreground = isTree ? activeFg : inactiveFg;
        treeViewText.Foreground = isTree ? activeFg : inactiveFg;
    }

    private void RefreshCurrentView()
    {
        if (currentMode == CatalogViewMode.Tree)
        {
            treeViewControl = BuildTreeView();
            mainContentHost.Content = treeViewControl;
        }
        else if (currentMode == CatalogViewMode.Flat)
        {
            flatViewControl = BuildFlatView();
            mainContentHost.Content = flatViewControl;
        }
    }

    #region Flat View

    private Control BuildFlatView()
    {
        var filteredItems = FilterItems(allFlatItems);

        if (filteredItems.Count == 0)
        {
            return CreateEmptyState(string.IsNullOrEmpty(filterQuery)
                ? "No se encontraron recursos ni estilos en este archivo."
                : $"No se encontraron recursos que coincidan con '{filterQuery}'.");
        }

        var scroll = new ScrollViewer
        {
            Padding = new Thickness(24, 20),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var rootStack = new StackPanel
        {
            Spacing = 28,
            MaxWidth = 1400,
            HorizontalAlignment = HorizontalAlignment.Left
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

        // 4. Other Resources
        var others = filteredItems.Where(i => i.Kind == ResourceItemKind.Other).ToList();
        if (others.Count > 0)
        {
            rootStack.Children.Add(CreateCategorySection("📦", "Otros Recursos", others));
        }

        scroll.Content = rootStack;
        return scroll;
    }

    private Control CreateCategorySection(string icon, string title, List<ResourceItemModel> items)
    {
        var section = new StackPanel
        {
            Spacing = 12
        };

        // Header
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

        // WrapPanel of cards
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

    #endregion

    #region Tree View

    private Control BuildTreeView()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,1,*")
        };

        // Left: TreeView
        var treeBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0F172A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12)
        };

        var treeView = new TreeView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var rootNode = CreateTreeNode(rootGroup);
        if (rootNode != null)
        {
            treeView.Items.Add(rootNode);
            rootNode.IsExpanded = true;
        }

        treeBorder.Child = treeView;
        Grid.SetColumn(treeBorder, 0);
        grid.Children.Add(treeBorder);

        // Separator
        var separator = new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Color.Parse("#1E293B"))
        };
        Grid.SetColumn(separator, 1);
        grid.Children.Add(separator);

        // Right: Detail pane
        var detailScroll = new ScrollViewer
        {
            Padding = new Thickness(24, 20),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        treeDetailPanel = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 1200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        detailScroll.Content = treeDetailPanel;
        Grid.SetColumn(detailScroll, 2);
        grid.Children.Add(detailScroll);

        // Show root items by default
        selectedGroup = rootGroup;
        UpdateTreeDetailPane(rootGroup.Items);

        return grid;
    }

    private TreeViewItem? CreateTreeNode(ResourceGroupModel group)
    {
        var matchingItems = FilterItems(group.Items);
        var subNodes = new List<TreeViewItem>();

        foreach (var sub in group.Subgroups)
        {
            var node = CreateTreeNode(sub);
            if (node != null)
            {
                subNodes.Add(node);
            }
        }

        if (matchingItems.Count == 0 && subNodes.Count == 0 && !string.IsNullOrEmpty(filterQuery))
        {
            return null; // Excluded by filter
        }

        var tvi = new TreeViewItem
        {
            Header = CreateTreeNodeHeader(group.Icon, group.Title, matchingItems.Count + subNodes.Sum(s => s.Items.Count)),
            IsExpanded = true
        };

        // Categorized children inside this group
        if (matchingItems.Count > 0)
        {
            // By kind subnodes if top-level or mixed
            var byKind = matchingItems.GroupBy(i => i.Kind);
            foreach (var g in byKind)
            {
                var kindTitle = GetKindTitle(g.Key);
                var kindIcon = GetKindIcon(g.Key);
                var kindNode = new TreeViewItem
                {
                    Header = CreateTreeNodeHeader(kindIcon, kindTitle, g.Count()),
                    IsExpanded = true
                };

                var itemsInKind = g.ToList();
                kindNode.PointerPressed += (_, _) =>
                {
                    UpdateTreeDetailPane(itemsInKind, $"{group.Title} > {kindTitle}");
                };

                foreach (var item in itemsInKind)
                {
                    var itemNode = new TreeViewItem
                    {
                        Header = CreateTreeItemHeader(item)
                    };
                    itemNode.PointerPressed += (s, e) =>
                    {
                        e.Handled = true;
                        UpdateTreeDetailSingleItem(item);
                    };
                    kindNode.Items.Add(itemNode);
                }

                tvi.Items.Add(kindNode);
            }
        }

        foreach (var sub in subNodes)
        {
            tvi.Items.Add(sub);
        }

        tvi.PointerPressed += (_, _) =>
        {
            UpdateTreeDetailPane(matchingItems, group.Title);
        };

        return tvi;
    }

    private static Control CreateTreeNodeHeader(string icon, string title, int count)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(new TextBlock { Text = icon, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = title, FontSize = 12.5, FontWeight = FontWeight.Medium, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        if (count > 0)
        {
            stack.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1E293B")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1.5),
                Child = new TextBlock
                {
                    Text = count.ToString(),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.Parse("#94A3B8"))
                }
            });
        }
        return stack;
    }

    private static Control CreateTreeItemHeader(ResourceItemModel item)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(new TextBlock { Text = GetKindIcon(item.Kind), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        stack.Children.Add(new TextBlock
        {
            Text = item.KeyOrSelector,
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.Parse("#E2E8F0")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 200,
            VerticalAlignment = VerticalAlignment.Center
        });
        return stack;
    }

    private void UpdateTreeDetailPane(List<ResourceItemModel> items, string? path = null)
    {
        if (treeDetailPanel == null) return;
        treeDetailPanel.Children.Clear();

        if (!string.IsNullOrEmpty(path))
        {
            treeDetailPanel.Children.Add(new TextBlock
            {
                Text = path,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#38BDF8")),
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        if (items.Count == 0)
        {
            treeDetailPanel.Children.Add(new TextBlock
            {
                Text = "Selecciona una categoría o elemento en el árbol para ver su previsualización.",
                Foreground = new SolidColorBrush(Color.Parse("#64748B")),
                FontSize = 12
            });
            return;
        }

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var item in items)
        {
            wrap.Children.Add(CreateResourceCard(item));
        }
        treeDetailPanel.Children.Add(wrap);
    }

    private void UpdateTreeDetailSingleItem(ResourceItemModel item)
    {
        if (treeDetailPanel == null) return;
        treeDetailPanel.Children.Clear();

        var card = CreateResourceCard(item);
        card.Width = 460; // Larger detailed view
        treeDetailPanel.Children.Add(card);
    }

    #endregion

    #region Card Builder

    private Control CreateResourceCard(ResourceItemModel item)
    {
        var card = new Border
        {
            Width = 280,
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

        // Header: Badge + Label + Origin
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

        // Kind pill
        topRow.Children.Add(CreateKindBadge(item.Kind));

        // Origin pill if nested
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

        // Label / Selector / Key
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

        // Center: Live Preview Control
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

    #endregion

    #region Helpers

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

    private static string GetKindTitle(ResourceItemKind kind) => kind switch
    {
        ResourceItemKind.Style => "Estilos (Styles)",
        ResourceItemKind.ControlTheme => "Temas (ControlThemes)",
        ResourceItemKind.Brush => "Pinceles (Brushes)",
        ResourceItemKind.Color => "Colores",
        _ => "Otros Recursos"
    };

    private static string GetKindIcon(ResourceItemKind kind) => kind switch
    {
        ResourceItemKind.Style => "🪄",
        ResourceItemKind.ControlTheme => "🎛",
        ResourceItemKind.Brush => "🖌",
        ResourceItemKind.Color => "🎨",
        _ => "📦"
    };

    private static Control CreateEmptyState(string message)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12
        };
        panel.Children.Add(new TextBlock
        {
            Text = "🔍",
            FontSize = 36,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        return panel;
    }

    #endregion
}
