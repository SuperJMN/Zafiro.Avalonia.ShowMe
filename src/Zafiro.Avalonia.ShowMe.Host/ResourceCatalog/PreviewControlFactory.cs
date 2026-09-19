using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

public static class PreviewControlFactory
{
    public static Control CreatePreviewForTheme(ControlTheme theme, Assembly? targetAssembly)
    {
        var targetType = theme.TargetType;
        if (targetType == null)
        {
            return new TextBlock { Text = "ControlTheme (Sin TargetType)", Foreground = Brushes.Gray };
        }

        try
        {
            Control control;

            if (typeof(Button).IsAssignableFrom(targetType))
            {
                control = new Button
                {
                    Content = "Button",
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(TextBox).IsAssignableFrom(targetType))
            {
                control = new TextBox
                {
                    Text = "Sample Text",
                    Theme = theme,
                    Width = 180,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(CheckBox).IsAssignableFrom(targetType))
            {
                control = new CheckBox
                {
                    Content = "CheckBox",
                    IsChecked = true,
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(RadioButton).IsAssignableFrom(targetType))
            {
                control = new RadioButton
                {
                    Content = "RadioButton",
                    IsChecked = true,
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(ToggleSwitch).IsAssignableFrom(targetType))
            {
                control = new ToggleSwitch
                {
                    IsChecked = true,
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(Slider).IsAssignableFrom(targetType))
            {
                control = new Slider
                {
                    Value = 50,
                    Minimum = 0,
                    Maximum = 100,
                    Width = 160,
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(ProgressBar).IsAssignableFrom(targetType))
            {
                control = new ProgressBar
                {
                    Value = 60,
                    Minimum = 0,
                    Maximum = 100,
                    Width = 160,
                    Height = 10,
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(TextBlock).IsAssignableFrom(targetType))
            {
                control = new TextBlock
                {
                    Text = "Sample TextBlock",
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (typeof(Border).IsAssignableFrom(targetType))
            {
                control = new Border
                {
                    Width = 100,
                    Height = 36,
                    Background = new SolidColorBrush(Color.Parse("#38BDF8")),
                    CornerRadius = new CornerRadius(6),
                    Theme = theme,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                if (Activator.CreateInstance(targetType) is Control custom)
                {
                    custom.Theme = theme;
                    custom.HorizontalAlignment = HorizontalAlignment.Center;
                    custom.VerticalAlignment = VerticalAlignment.Center;
                    if (custom is ContentControl cc && cc.Content == null)
                    {
                        cc.Content = targetType.Name;
                    }
                    control = custom;
                }
                else
                {
                    control = new TextBlock { Text = targetType.Name, Foreground = Brushes.Gray };
                }
            }

            return WrapInPreviewBox(control);
        }
        catch (Exception)
        {
            return WrapInPreviewBox(new TextBlock { Text = $"[Theme: {targetType.Name}]", Foreground = Brushes.LightSlateGray });
        }
    }

    public static Control CreatePreviewForStyle(Style style, Assembly? targetAssembly, Styles? parentStyles)
    {
        var selectorStr = style.Selector?.ToString() ?? "Style";
        var (typeName, classes) = ParseSelector(selectorStr);

        var targetType = ResolveControlType(typeName, targetAssembly);
        Control control;

        try
        {
            control = (Control)Activator.CreateInstance(targetType)!;
            foreach (var cls in classes)
            {
                control.Classes.Add(cls);
            }

            if (control is Button btn)
            {
                btn.Content = classes.Count > 0 ? $"Button .{string.Join(".", classes)}" : "Button";
                btn.HorizontalAlignment = HorizontalAlignment.Center;
                btn.VerticalAlignment = VerticalAlignment.Center;
            }
            else if (control is TextBlock tbl)
            {
                tbl.Text = classes.Count > 0 ? $"TextBlock .{string.Join(".", classes)}" : "Sample TextBlock";
                tbl.TextTrimming = TextTrimming.CharacterEllipsis;
                tbl.MaxWidth = 230;
                tbl.HorizontalAlignment = HorizontalAlignment.Center;
                tbl.VerticalAlignment = VerticalAlignment.Center;
            }
            else if (control is TextBox tb)
            {
                tb.Text = classes.Count > 0 ? $"TextBox .{string.Join(".", classes)}" : "Sample TextBox";
                tb.Width = 180;
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
            }
            else if (control is Border bdr)
            {
                bdr.Width = 120;
                bdr.Height = 40;
                bdr.CornerRadius = new CornerRadius(4);
                bdr.Child = new TextBlock
                {
                    Text = classes.Count > 0 ? $".{string.Join(".", classes)}" : "Border",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (control is ContentControl cc && cc.Content == null)
            {
                cc.Content = targetType.Name;
                cc.HorizontalAlignment = HorizontalAlignment.Center;
                cc.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                control.HorizontalAlignment = HorizontalAlignment.Center;
                control.VerticalAlignment = VerticalAlignment.Center;
            }

            return WrapInPreviewBox(control);
        }
        catch
        {
            return WrapInPreviewBox(new TextBlock { Text = $"[Style: {selectorStr}]", Foreground = Brushes.Gray });
        }
    }

    public static Control CreatePreviewForBrush(IBrush brush, string key)
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var swatch = new Border
        {
            Width = 120,
            Height = 38,
            CornerRadius = new CornerRadius(6),
            Background = brush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 6, Spread = 0, Color = Color.FromArgb(40, 0, 0, 0), OffsetY = 2 })
        };
        panel.Children.Add(swatch);

        if (brush is ISolidColorBrush scb)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"#{scb.Color.A:X2}{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}",
                FontSize = 10.5,
                FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        return WrapInPreviewBox(panel);
    }

    public static Control CreatePreviewForColor(Color color, string key)
    {
        return CreatePreviewForBrush(new SolidColorBrush(color), key);
    }

    public static Control CreatePreviewForOther(object? value, string key)
    {
        if (value is BoxShadows shadows)
        {
            var bdr = new Border
            {
                Width = 100,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse("#1E293B")),
                BoxShadow = shadows,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return WrapInPreviewBox(bdr);
        }

        if (value is CornerRadius cr)
        {
            var bdr = new Border
            {
                Width = 90,
                Height = 36,
                CornerRadius = cr,
                Background = new SolidColorBrush(Color.Parse("#0EA5E9")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return WrapInPreviewBox(bdr);
        }

        if (value is FontFamily ff)
        {
            var tb = new TextBlock
            {
                Text = "Aa Bb Gg 123",
                FontFamily = ff,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return WrapInPreviewBox(tb);
        }

        return WrapInPreviewBox(new TextBlock
        {
            Text = value?.ToString() ?? "(null)",
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#CBD5E1")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    private static Border WrapInPreviewBox(Control child)
    {
        return new Border
        {
            Padding = new Thickness(12, 10),
            MinHeight = 56,
            MaxHeight = 110,
            ClipToBounds = true,
            MinWidth = 130,
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = child
        };
    }

    private static (string typeName, List<string> classes) ParseSelector(string selectorStr)
    {
        var firstToken = selectorStr.Split(new[] { ' ', '>', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var colonIdx = firstToken.IndexOf(':');
        if (colonIdx >= 0)
        {
            firstToken = firstToken.Substring(0, colonIdx);
        }

        var parts = firstToken.Split('.');
        var typeName = parts[0];
        var pipeIdx = typeName.IndexOf('|');
        if (pipeIdx >= 0)
        {
            typeName = typeName.Substring(pipeIdx + 1);
        }

        var classes = parts.Skip(1).Where(c => !string.IsNullOrEmpty(c)).ToList();
        return (typeName, classes);
    }

    private static Type ResolveControlType(string typeName, Assembly? targetAssembly)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return typeof(Button);
        }

        var standardMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["Button"] = typeof(Button),
            ["TextBlock"] = typeof(TextBlock),
            ["TextBox"] = typeof(TextBox),
            ["Border"] = typeof(Border),
            ["CheckBox"] = typeof(CheckBox),
            ["RadioButton"] = typeof(RadioButton),
            ["ToggleSwitch"] = typeof(ToggleSwitch),
            ["Slider"] = typeof(Slider),
            ["ProgressBar"] = typeof(ProgressBar),
            ["Image"] = typeof(Image),
            ["PathIcon"] = typeof(PathIcon),
            ["StackPanel"] = typeof(StackPanel),
            ["Grid"] = typeof(Grid),
            ["Panel"] = typeof(Panel),
            ["ListBox"] = typeof(ListBox),
            ["ComboBox"] = typeof(ComboBox),
            ["TabControl"] = typeof(TabControl),
            ["TabItem"] = typeof(TabItem),
            ["Calendar"] = typeof(Calendar),
            ["DatePicker"] = typeof(DatePicker),
            ["TimePicker"] = typeof(TimePicker),
            ["ScrollViewer"] = typeof(ScrollViewer),
            ["Separator"] = typeof(Separator)
        };

        if (standardMap.TryGetValue(typeName, out var type))
        {
            return type;
        }

        if (targetAssembly != null)
        {
            try
            {
                var match = targetAssembly.GetTypes().FirstOrDefault(t =>
                    string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase) &&
                    typeof(Control).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t.GetConstructor(Type.EmptyTypes) != null);

                if (match != null) return match;
            }
            catch { }
        }

        return typeof(Button);
    }
}
