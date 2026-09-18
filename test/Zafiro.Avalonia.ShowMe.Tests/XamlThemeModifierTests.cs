using Xunit;
using Zafiro.Avalonia.ShowMe.Core;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class XamlThemeModifierTests
{
    [Fact]
    public void Default_Theme_Returns_Original_Xaml()
    {
        var xaml = "<UserControl xmlns=\"https://github.com/avaloniaui\"><TextBlock Text=\"Hello\" /></UserControl>";
        var result = XamlThemeModifier.ApplyTheme(xaml, "Default");
        Assert.Equal(xaml, result);
    }

    [Fact]
    public void Dark_Theme_Sets_RequestedThemeVariant_On_Window()
    {
        var xaml = "<Window xmlns=\"https://github.com/avaloniaui\" Title=\"Test\"><TextBlock Text=\"Hello\" /></Window>";
        var result = XamlThemeModifier.ApplyTheme(xaml, "Dark");

        Assert.Contains("RequestedThemeVariant=\"Dark\"", result);
    }

    [Fact]
    public void Light_Theme_Sets_RequestedThemeVariant_On_Window()
    {
        var xaml = "<Window xmlns=\"https://github.com/avaloniaui\" Title=\"Test\"><TextBlock Text=\"Hello\" /></Window>";
        var result = XamlThemeModifier.ApplyTheme(xaml, "Light");

        Assert.Contains("RequestedThemeVariant=\"Light\"", result);
    }

    [Fact]
    public void Dark_Theme_Wraps_UserControl_Contents_In_ThemeVariantScope()
    {
        var xaml = """
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <TextBlock Text="Hello" />
                </StackPanel>
            </UserControl>
            """;

        var result = XamlThemeModifier.ApplyTheme(xaml, "Dark");

        Assert.Contains("ThemeVariantScope", result);
        Assert.Contains("RequestedThemeVariant=\"Dark\"", result);
    }
}
