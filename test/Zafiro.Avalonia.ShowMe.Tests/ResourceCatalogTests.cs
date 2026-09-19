using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;
using Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class ResourceCatalogTests
{
    [Fact]
    public void Extractor_ShouldExtract_StylesWithSelectors()
    {
        var styles = new Styles();
        var style1 = new Style(x => x.OfType<Button>().Class("Red"));
        var style2 = new Style(x => x.OfType<TextBox>());

        styles.Add(style1);
        styles.Add(style2);

        var group = ResourceExtractor.Extract(styles, null, "TestStyles.axaml");

        Assert.NotNull(group);
        var flat = ResourceExtractor.GetAllFlatItems(group);

        Assert.Equal(2, flat.Count);
        Assert.Contains(flat, i => i.Kind == ResourceItemKind.Style && i.KeyOrSelector.Contains("Button.Red"));
        Assert.Contains(flat, i => i.Kind == ResourceItemKind.Style && i.KeyOrSelector.Contains("TextBox"));
    }

    [Fact]
    public void Extractor_ShouldExtract_ControlThemes()
    {
        var styles = new Styles();
        var theme = new ControlTheme(typeof(Button));
        styles.Resources.Add(typeof(Button), theme);

        var group = ResourceExtractor.Extract(styles, null, "ThemeTest.axaml");
        var flat = ResourceExtractor.GetAllFlatItems(group);

        Assert.Contains(flat, i => i.Kind == ResourceItemKind.ControlTheme);
    }

    [Fact]
    public void Extractor_ShouldExtract_BrushesAndColors()
    {
        var dict = new ResourceDictionary
        {
            ["PrimaryBrush"] = new SolidColorBrush(Colors.DodgerBlue),
            ["AccentColor"] = Colors.OrangeRed
        };

        var group = ResourceExtractor.Extract(dict, null, "Colors.axaml");
        var flat = ResourceExtractor.GetAllFlatItems(group);

        Assert.Contains(flat, i => i.Kind == ResourceItemKind.Brush && i.KeyOrSelector == "PrimaryBrush");
        Assert.Contains(flat, i => i.Kind == ResourceItemKind.Color && i.KeyOrSelector == "AccentColor");
    }

    [Fact]
    public void Extractor_ShouldExtract_ThemeDictionaries()
    {
        var dict = new ResourceDictionary();
        var darkDict = new ResourceDictionary
        {
            ["BackgroundBrush"] = new SolidColorBrush(Colors.Black)
        };
        var lightDict = new ResourceDictionary
        {
            ["BackgroundBrush"] = new SolidColorBrush(Colors.White)
        };

        dict.ThemeDictionaries[ThemeVariant.Dark] = darkDict;
        dict.ThemeDictionaries[ThemeVariant.Light] = lightDict;

        var group = ResourceExtractor.Extract(dict, null, "Themes.axaml");
        Assert.NotEmpty(group.Subgroups);

        var flat = ResourceExtractor.GetAllFlatItems(group);
        Assert.Equal(2, flat.Count);
    }

    [Fact]
    public void Factory_ShouldGenerate_PreviewForStyle()
    {
        var style = new Style(x => x.OfType<Button>().Class("Danger"));
        var control = PreviewControlFactory.CreatePreviewForStyle(style, null, null);

        Assert.NotNull(control);
    }

    [Fact]
    public void Factory_ShouldGenerate_PreviewForControlTheme()
    {
        var theme = new ControlTheme(typeof(Button));
        var control = PreviewControlFactory.CreatePreviewForTheme(theme, null);

        Assert.NotNull(control);
    }

    [Fact]
    public void XamlLoader_StylesWithPreviewWith_CanBeExtracted()
    {
        string xaml = """
        <Styles xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Design.PreviewWith>
                <Border Padding="20">
                    <Button Classes="CustomBtn" Content="Click Me" />
                </Border>
            </Design.PreviewWith>
            <Style Selector="Button.CustomBtn">
                <Setter Property="FontSize" Value="24" />
            </Style>
        </Styles>
        """;

        var loaded = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, null, null, null, true);
        Assert.NotNull(loaded);
        Assert.IsType<Styles>(loaded);

        var styles = (Styles)loaded;
        var group = ResourceExtractor.Extract(styles, null, "CustomStyles.axaml");
        var flat = ResourceExtractor.GetAllFlatItems(group);

        Assert.Single(flat);
        Assert.Equal("Button.CustomBtn", flat[0].KeyOrSelector);

        // Check if Design.GetPreviewWith works on Styles
        var preview = Design.GetPreviewWith((AvaloniaObject)styles);
        Assert.NotNull(preview);
        Assert.IsType<Border>(preview);
    }

    [Fact]
    public void XamlLoader_ResourceDictionary_CanBeExtracted()
    {
        string xaml = """
        <ResourceDictionary xmlns="https://github.com/avaloniaui"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Design.PreviewWith>
                <Border Padding="10">
                    <TextBlock Text="Preview in Dict" />
                </Border>
            </Design.PreviewWith>
            <SolidColorBrush x:Key="BrandColor">#FF00FF</SolidColorBrush>
            <CornerRadius x:Key="DefaultRadius">8</CornerRadius>
        </ResourceDictionary>
        """;

        var loaded = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, null, null, null, true);
        Assert.NotNull(loaded);
        Assert.IsAssignableFrom<IResourceDictionary>(loaded);

        var dict = (IResourceDictionary)loaded;
        var group = ResourceExtractor.Extract(dict, null, "Resources.axaml");
        var flat = ResourceExtractor.GetAllFlatItems(group);

        Assert.Equal(2, flat.Count);
        Assert.Contains(flat, i => i.KeyOrSelector == "BrandColor");
        Assert.Contains(flat, i => i.KeyOrSelector == "DefaultRadius");

        if (dict is AvaloniaObject avObj)
        {
            var preview = Design.GetPreviewWith(avObj);
            Assert.NotNull(preview);
        }
    }

    [Fact]
    public void Proteus_ColorsAxaml_Extracts_AllBrushesAndColors_AndBuildsCatalog()
    {
        var path = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/Colors.axaml";
        if (!File.Exists(path)) return;

        var xaml = File.ReadAllText(path);
        var loaded = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, null, null, null, true);
        Assert.NotNull(loaded);
        Assert.IsAssignableFrom<IResourceDictionary>(loaded);

        var catalog = ResourceCatalogBuilder.BuildCatalog(loaded, null, "Colors.axaml");
        Assert.NotNull(catalog);
        Assert.IsType<ResourceCatalogView>(catalog);

        var group = ResourceExtractor.Extract(loaded, null, "Colors.axaml");
        var flat = ResourceExtractor.GetAllFlatItems(group);
        Assert.True(flat.Count > 10, $"Expected many colors and brushes, got {flat.Count}");
    }

    [Fact]
    public void Proteus_TypographyStylesAxaml_Extracts_Styles_AndBuildsCatalog()
    {
        var path = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/TypographyStyles.axaml";
        if (!File.Exists(path)) return;

        var xaml = File.ReadAllText(path);
        var loaded = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, null, null, null, true);
        Assert.NotNull(loaded);
        Assert.IsType<Styles>(loaded);

        var catalog = ResourceCatalogBuilder.BuildCatalog(loaded, null, "TypographyStyles.axaml");
        Assert.NotNull(catalog);
        Assert.IsType<ResourceCatalogView>(catalog);

        var group = ResourceExtractor.Extract(loaded, null, "TypographyStyles.axaml");
        var flat = ResourceExtractor.GetAllFlatItems(group);
        Assert.True(flat.Count > 10, $"Expected many typography styles, got {flat.Count}");
    }

    [Fact]
    public void Proteus_BorderRadiusAxaml_Extracts_CornerRadii_AndBuildsCatalog()
    {
        var path = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/BorderRadius.axaml";
        if (!File.Exists(path)) return;

        var xaml = File.ReadAllText(path);
        var loaded = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, null, null, null, true);
        var catalog = ResourceCatalogBuilder.BuildCatalog(loaded, null, "BorderRadius.axaml");
        Assert.NotNull(catalog);
        Assert.IsType<ResourceCatalogView>(catalog);
    }

    [Fact]
    public void ResourceCatalogView_SwitchToTreeView_DoesNotCrash()
    {
        var path = "/home/jmn/Repos/proteus-ui/Proteus.Ui.Theme/DesignTokens/Colors.axaml";
        if (!File.Exists(path)) return;

        var xaml = File.ReadAllText(path);
        var loaded = global::Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml, null, null, null, true);
        var catalog = (ResourceCatalogView)ResourceCatalogBuilder.BuildCatalog(loaded, null, "Colors.axaml");

        catalog.SwitchToTreeView();
    }
}
