using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
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

        if (IsTypographyStyle(typeName, classes, style))
        {
            return CreateTypographyPreview(style, typeName, classes, selectorStr);
        }

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
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 1. Superficie con el motivo (Surface)
        var surfaceContainer = new Panel
        {
            Height = 74,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Capa 0: Fondo checkerboard sutil para ver transparencias y alphas
        var bgChecker = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = CreateCheckerboardBrush(),
            ClipToBounds = true
        };
        surfaceContainer.Children.Add(bgChecker);

        // Capa 1: Superficie con el motivo del brush
        var surface = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = brush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 6,
                Spread = 0,
                Color = Color.FromArgb(40, 0, 0, 0),
                OffsetY = 2
            })
        };
        surfaceContainer.Children.Add(surface);

        panel.Children.Add(surfaceContainer);

        // 2. Ficha de detalles del motivo debajo de la superficie
        if (brush is ISolidColorBrush scb)
        {
            var hex = $"#{scb.Color.A:X2}{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
            var detailText = scb.Color.A < 255
                ? $"{hex} · {scb.Color.A / 255.0 * 100:0}% opacidad"
                : hex;

            panel.Children.Add(new TextBlock
            {
                Text = detailText,
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#CBD5E1")),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        else if (brush is IGradientBrush gb)
        {
            var gradientType = brush switch
            {
                LinearGradientBrush lgb => $"Linear ({FormatRelPoint(lgb.StartPoint)} → {FormatRelPoint(lgb.EndPoint)})",
                RadialGradientBrush rgb => $"Radial (Center: {FormatRelPoint(rgb.Center)})",
                ConicGradientBrush cgb => $"Conic ({cgb.Angle}°)",
                _ => "Gradient"
            };

            var infoPanel = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            infoPanel.Children.Add(new TextBlock
            {
                Text = gradientType,
                FontSize = 9.5,
                FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // Fila de chips con los GradientStops
            if (gb.GradientStops.Count > 0)
            {
                var stopsRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                foreach (var stop in gb.GradientStops)
                {
                    var stopChip = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 3,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var dot = new Border
                    {
                        Width = 9,
                        Height = 9,
                        CornerRadius = new CornerRadius(4.5),
                        Background = new SolidColorBrush(stop.Color),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var stopLabel = new TextBlock
                    {
                        Text = $"{(stop.Offset * 100):0.#}%",
                        FontSize = 9,
                        FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                        Foreground = new SolidColorBrush(Color.Parse("#CBD5E1")),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    stopChip.Children.Add(dot);
                    stopChip.Children.Add(stopLabel);
                    stopsRow.Children.Add(stopChip);
                }

                infoPanel.Children.Add(stopsRow);
            }

            panel.Children.Add(infoPanel);
        }
        else if (brush is ITileBrush tb)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"TileBrush · {tb.TileMode}",
                FontSize = 10,
                FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        return WrapInPreviewBox(panel);
    }

    public static Control CreatePreviewForGeometry(Geometry geometry, string? key = null)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };

        var surface = new Border
        {
            Height = 74,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#0B132B")),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 6,
                Spread = 0,
                Color = Color.FromArgb(40, 0, 0, 0),
                OffsetY = 2
            })
        };

        var path = new global::Avalonia.Controls.Shapes.Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Color.Parse("#38BDF8")), // Cyan acento para iconos y vectores
            Stretch = Stretch.Uniform,
            Width = 44,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        surface.Child = path;
        panel.Children.Add(surface);

        var bounds = geometry.Bounds;
        var infoText = bounds.Width > 0 && bounds.Height > 0
            ? $"Vector ({bounds.Width:0.#} × {bounds.Height:0.#} px)"
            : "Vector Path";

        panel.Children.Add(new TextBlock
        {
            Text = infoText,
            FontSize = 10,
            FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
            HorizontalAlignment = HorizontalAlignment.Center
        });

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
            Padding = new Thickness(8, 6),
            MinHeight = 64,
            MaxHeight = 140,
            ClipToBounds = true,
            MinWidth = 130,
            Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = child
        };
    }

    private static IBrush CreateCheckerboardBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing
        {
            Brush = new SolidColorBrush(Color.Parse("#0F172A")),
            Geometry = new RectangleGeometry(new Rect(0, 0, 16, 16))
        });

        var altBrush = new SolidColorBrush(Color.Parse("#1E293B"));
        group.Children.Add(new GeometryDrawing
        {
            Brush = altBrush,
            Geometry = new RectangleGeometry(new Rect(0, 0, 8, 8))
        });
        group.Children.Add(new GeometryDrawing
        {
            Brush = altBrush,
            Geometry = new RectangleGeometry(new Rect(8, 8, 8, 8))
        });

        return new DrawingBrush
        {
            Drawing = group,
            DestinationRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
            TileMode = TileMode.Tile
        };
    }

    private static string FormatRelPoint(RelativePoint rp)
    {
        if (rp.Unit == RelativeUnit.Relative)
        {
            return $"{rp.Point.X * 100:0.#}%,{rp.Point.Y * 100:0.#}%";
        }
        return $"{rp.Point.X:0.#},{rp.Point.Y:0.#}";
    }

    private static bool IsTypographyStyle(string typeName, List<string> classes, Style style)
    {
        if (string.Equals(typeName, "TextBlock", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "Run", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "Paragraph", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "Span", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "Inline", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "SelectableTextBlock", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "TextPresenter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "AccessText", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(typeName, "TextBox", StringComparison.OrdinalIgnoreCase))
        {
            if (classes.Any(c => c.Contains("Typography", StringComparison.OrdinalIgnoreCase) ||
                                 c.Contains("Heading", StringComparison.OrdinalIgnoreCase) ||
                                 c.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                                 c.Contains("Caption", StringComparison.OrdinalIgnoreCase) ||
                                 c.Contains("Font", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (classes.Any(c => c.Contains("Typography", StringComparison.OrdinalIgnoreCase) ||
                             c.Contains("Heading", StringComparison.OrdinalIgnoreCase) ||
                             c.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
                             c.Contains("Caption", StringComparison.OrdinalIgnoreCase) ||
                             c.Contains("Lead", StringComparison.OrdinalIgnoreCase) ||
                             c.Contains("Display", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (var setter in style.Setters.OfType<Setter>())
        {
            var propName = setter.Property?.Name;
            if (propName is "FontSize" or "FontFamily" or "FontWeight" or "LineHeight" or "LetterSpacing")
            {
                return true;
            }
        }

        return false;
    }

    private static Control CreateTypographyPreview(Style style, string typeName, List<string> classes, string selectorStr)
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };

        var (fontFamily, fontSize, fontWeight, lineHeight) = ExtractTypographyMeta(style);

        var surface = new Border
        {
            MinHeight = 60,
            MaxHeight = 84,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#0F172A")),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6),
            ClipToBounds = true
        };

        string sampleText;
        if (classes.Any(c => c.Contains("Heading", StringComparison.OrdinalIgnoreCase) || c.Contains("Display", StringComparison.OrdinalIgnoreCase)) || (fontSize.HasValue && fontSize.Value >= 32))
        {
            sampleText = "Ag 123";
        }
        else
        {
            sampleText = "The quick brown fox jumps over the lazy dog";
        }

        Control textControl;

        if (string.Equals(typeName, "Run", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "Paragraph", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(typeName, "Span", StringComparison.OrdinalIgnoreCase))
        {
            var run = new global::Avalonia.Controls.Documents.Run(sampleText);
            foreach (var cls in classes)
            {
                run.Classes.Add(cls);
            }

            var textBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#F8FAFC")),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.Inlines!.Add(run);
            textControl = textBlock;
        }
        else if (string.Equals(typeName, "TextBox", StringComparison.OrdinalIgnoreCase))
        {
            var tb = new TextBox
            {
                Text = sampleText,
                Foreground = new SolidColorBrush(Color.Parse("#F8FAFC")),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var cls in classes)
            {
                tb.Classes.Add(cls);
            }
            textControl = tb;
        }
        else
        {
            var tbl = new TextBlock
            {
                Text = sampleText,
                Foreground = new SolidColorBrush(Color.Parse("#F8FAFC")),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var cls in classes)
            {
                tbl.Classes.Add(cls);
            }
            textControl = tbl;
        }

        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 70,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = textControl
        };
        surface.Child = viewbox;
        panel.Children.Add(surface);

        var metaItems = new List<string>();
        if (fontSize.HasValue) metaItems.Add($"{fontSize.Value:0.#}px");
        if (!string.IsNullOrEmpty(fontWeight)) metaItems.Add(fontWeight);
        if (lineHeight.HasValue) metaItems.Add($"LH {lineHeight.Value:0.#}");
        if (!string.IsNullOrEmpty(fontFamily)) metaItems.Add(fontFamily);

        if (metaItems.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", metaItems),
                FontSize = 9.5,
                FontFamily = FontFamily.Parse("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        return WrapInPreviewBox(panel);
    }

    private static (string? fontFamily, double? fontSize, string? fontWeight, double? lineHeight) ExtractTypographyMeta(Style style)
    {
        string? fontFamily = null;
        double? fontSize = null;
        string? fontWeight = null;
        double? lineHeight = null;

        foreach (var setter in style.Setters.OfType<Setter>())
        {
            var name = setter.Property?.Name;
            var val = setter.Value;

            string? resourceKey = null;
            if (val is global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension dre)
            {
                resourceKey = dre.ResourceKey?.ToString();
            }
            else if (val is global::Avalonia.Markup.Xaml.MarkupExtensions.StaticResourceExtension sre)
            {
                resourceKey = sre.ResourceKey?.ToString();
            }

            if (!string.IsNullOrEmpty(resourceKey) && Application.Current != null)
            {
                if (Application.Current.Resources.TryGetResource(resourceKey, null, out var resVal) && resVal != null)
                {
                    val = resVal;
                }
            }

            if (name == "FontSize")
            {
                if (val is double d) fontSize = d;
                else if (val != null && double.TryParse(val.ToString(), out var pd)) fontSize = pd;
            }
            else if (name == "FontFamily")
            {
                fontFamily = val is FontFamily ff ? ff.Name : val?.ToString();
            }
            else if (name == "FontWeight")
            {
                fontWeight = val?.ToString();
            }
            else if (name == "LineHeight")
            {
                if (val is double lh) lineHeight = lh;
                else if (val != null && double.TryParse(val.ToString(), out var plh)) lineHeight = plh;
            }
        }

        return (fontFamily, fontSize, fontWeight, lineHeight);
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
