using System.Text.Json;
using Xunit;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class CatalogViewModelsTests
{
    [Fact]
    public void CatalogItemViewModel_PropertiesAndFormatting_AreCorrect()
    {
        var dto = new CatalogItemDto(
            Id: "PrimaryBrush",
            KeyOrSelector: "PrimaryBrush",
            Kind: CatalogItemKindDto.Brush,
            GroupPath: "Colors",
            ValueSummary: "#FF007ACC"
        );

        var vm = new CatalogItemViewModel(dto);

        Assert.Equal("PrimaryBrush", vm.Id);
        Assert.Equal("PrimaryBrush", vm.KeyOrSelector);
        Assert.Equal(CatalogItemKindDto.Brush, vm.Kind);
        Assert.Equal("Brush", vm.KindText);
        Assert.Equal("🎨", vm.KindIcon);
        Assert.Equal("Brush · #FF007ACC", vm.DisplaySubtitle);
        Assert.NotNull(vm.ColorBrush);
    }

    [Fact]
    public void CatalogItemViewModel_Filtering_WorksAccurately()
    {
        var dto = new CatalogItemDto(
            Id: "Button.Primary",
            KeyOrSelector: "Button.Primary",
            Kind: CatalogItemKindDto.Style,
            GroupPath: "Buttons",
            ValueSummary: "Selector"
        );

        var vm = new CatalogItemViewModel(dto);

        Assert.True(vm.Matches("button"));
        Assert.True(vm.Matches("Primary"));
        Assert.True(vm.Matches("Style"));
        Assert.False(vm.Matches("CheckBox"));
    }

    [Fact]
    public void CatalogGroupNodeViewModel_HierarchicalFiltering_WorksCorrectly()
    {
        var item1 = new CatalogItemDto("RedBrush", "RedBrush", CatalogItemKindDto.Brush, "Colors", "#FFFF0000");
        var item2 = new CatalogItemDto("BlueBrush", "BlueBrush", CatalogItemKindDto.Brush, "Colors", "#FF0000FF");
        var group = new CatalogGroupDto("Colors", "📁", "Colors.axaml", new List<CatalogItemDto> { item1, item2 }, new List<CatalogGroupDto>());

        var vm = new CatalogGroupNodeViewModel(group);

        Assert.Equal(2, vm.TotalItemCount);
        Assert.Equal(2, vm.Children.Count);

        // Filter for "Red"
        bool matches = vm.ApplyFilter("Red");
        Assert.True(matches);
        Assert.True(vm.IsVisible);
        Assert.Single(vm.Children); // Only RedBrush visible
        Assert.Equal("RedBrush", ((CatalogItemViewModel)vm.Children[0]).KeyOrSelector);

        // Clear filter
        vm.ApplyFilter("");
        Assert.Equal(2, vm.Children.Count);
    }

    [Fact]
    public void Messages_CatalogSerialization_WorksCorrectly()
    {
        var itemDto = new CatalogItemDto("BtnTheme", "BtnTheme", CatalogItemKindDto.ControlTheme, "Themes", "ControlTheme");
        var groupDto = new CatalogGroupDto("Themes", "📁", "Themes.axaml", new List<CatalogItemDto> { itemDto }, new List<CatalogGroupDto>());
        var catalogInfo = new CatalogInfoMessage("Themes.axaml", true, groupDto, new List<CatalogItemDto> { itemDto });

        var json = JsonSerializer.Serialize<ShowMeMessage>(catalogInfo);
        var deserialized = JsonSerializer.Deserialize<ShowMeMessage>(json);

        Assert.NotNull(deserialized);
        var res = Assert.IsType<CatalogInfoMessage>(deserialized);
        Assert.Equal("Themes.axaml", res.Title);
        Assert.True(res.HasPreviewWith);
        Assert.Single(res.AllItems);
        Assert.Equal("BtnTheme", res.AllItems[0].KeyOrSelector);

        var selectMsg = new SelectCatalogItemMessage("BtnTheme", "Single", null);
        var jsonSelect = JsonSerializer.Serialize<ShowMeMessage>(selectMsg);
        var deserializedSelect = JsonSerializer.Deserialize<ShowMeMessage>(jsonSelect);

        Assert.NotNull(deserializedSelect);
        var selectRes = Assert.IsType<SelectCatalogItemMessage>(deserializedSelect);
        Assert.Equal("BtnTheme", selectRes.ItemId);
        Assert.Equal("Single", selectRes.ViewMode);
    }
}
