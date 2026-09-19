using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

public enum ResourceItemKind
{
    ControlTheme,
    Style,
    Brush,
    Color,
    Template,
    Other
}

public sealed class ResourceItemModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ResourceItemKind Kind { get; init; }
    public string Label { get; init; } = "";
    public string KeyOrSelector { get; init; } = "";
    public string TargetTypeName { get; init; } = "";
    public string? OriginPath { get; init; } // e.g. "Theme: Dark" or "Merged: Buttons.axaml"
    public object? RawValue { get; init; }
    public Func<Control>? PreviewControlFactory { get; init; }

    public Control CreatePreviewControl() => PreviewControlFactory?.Invoke() ?? new TextBlock { Text = Label };
    public Control GetPreviewControl() => CreatePreviewControl();
}

public sealed class ResourceGroupModel
{
    public string Title { get; init; } = "";
    public string Icon { get; init; } = "📁";
    public string? OriginPath { get; init; }
    public List<ResourceItemModel> Items { get; } = new();
    public List<ResourceGroupModel> Subgroups { get; } = new();

    public int TotalItemCount => Items.Count + Subgroups.Sum(s => s.TotalItemCount);
}
