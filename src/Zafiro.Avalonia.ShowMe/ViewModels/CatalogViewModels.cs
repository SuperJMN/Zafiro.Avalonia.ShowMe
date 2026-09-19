using System.Collections.ObjectModel;
using Avalonia.Media;
using ReactiveUI;
using Zafiro.Avalonia.ShowMe.Protocol;

namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed class CatalogItemViewModel : ReactiveObject
{
    private bool isVisible = true;

    public string Id { get; }
    public string KeyOrSelector { get; }
    public CatalogItemKindDto Kind { get; }
    public string KindText { get; }
    public string KindIcon { get; }
    public string? ValueSummary { get; }
    public string? GroupPath { get; }
    public IBrush? ColorBrush { get; }
    public string DisplaySubtitle { get; }

    public bool IsVisible
    {
        get => isVisible;
        set => this.RaiseAndSetIfChanged(ref isVisible, value);
    }

    public CatalogItemViewModel(CatalogItemDto dto)
    {
        Id = dto.Id;
        KeyOrSelector = dto.KeyOrSelector;
        Kind = dto.Kind;
        GroupPath = dto.GroupPath;
        ValueSummary = dto.ValueSummary;

        KindText = dto.Kind switch
        {
            CatalogItemKindDto.ControlTheme => "ControlTheme",
            CatalogItemKindDto.Style => "Style",
            CatalogItemKindDto.Brush => "Brush",
            CatalogItemKindDto.Color => "Color",
            CatalogItemKindDto.Template => "Template",
            CatalogItemKindDto.Geometry => "Geometría",
            _ => "Recurso"
        };

        KindIcon = dto.Kind switch
        {
            CatalogItemKindDto.ControlTheme => "🎛",
            CatalogItemKindDto.Style => "🪄",
            CatalogItemKindDto.Brush or CatalogItemKindDto.Color => "🎨",
            CatalogItemKindDto.Template => "📋",
            CatalogItemKindDto.Geometry => "📐",
            _ => "📦"
        };

        KindBadgeBrush = new SolidColorBrush(Color.Parse(dto.Kind switch
        {
            CatalogItemKindDto.ControlTheme => "#0EA5E9",
            CatalogItemKindDto.Style => "#8B5CF6",
            CatalogItemKindDto.Brush => "#10B981",
            CatalogItemKindDto.Color => "#F59E0B",
            CatalogItemKindDto.Geometry => "#0284C7",
            _ => "#64748B"
        }));

        if (!string.IsNullOrEmpty(dto.ValueSummary) && Color.TryParse(dto.ValueSummary, out var parsedCol))
        {
            ColorBrush = new SolidColorBrush(parsedCol);
        }

        DisplaySubtitle = !string.IsNullOrEmpty(dto.ValueSummary)
            ? $"{KindText} · {dto.ValueSummary}"
            : KindText;
    }

    public IBrush KindBadgeBrush { get; }

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return KeyOrSelector.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               KindText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrEmpty(ValueSummary) && ValueSummary.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CatalogGroupNodeViewModel : ReactiveObject
{
    private bool isExpanded = true;
    private bool isVisible = true;

    public string Title { get; }
    public string Icon { get; }
    public string? OriginPath { get; }
    public ObservableCollection<CatalogItemViewModel> Items { get; } = new();
    public ObservableCollection<CatalogGroupNodeViewModel> Subgroups { get; } = new();
    public ObservableCollection<object> Children { get; } = new();

    public int TotalItemCount => Items.Count + Subgroups.Sum(s => s.TotalItemCount);

    public bool IsExpanded
    {
        get => isExpanded;
        set => this.RaiseAndSetIfChanged(ref isExpanded, value);
    }

    public bool IsVisible
    {
        get => isVisible;
        set => this.RaiseAndSetIfChanged(ref isVisible, value);
    }

    public CatalogGroupNodeViewModel(CatalogGroupDto dto)
    {
        Title = dto.Title;
        Icon = dto.Icon;
        OriginPath = dto.OriginPath;

        foreach (var item in dto.Items)
        {
            Items.Add(new CatalogItemViewModel(item));
        }

        foreach (var sub in dto.Subgroups)
        {
            Subgroups.Add(new CatalogGroupNodeViewModel(sub));
        }

        RefreshChildren();
    }

    private void RefreshChildren()
    {
        Children.Clear();
        foreach (var sub in Subgroups)
        {
            if (sub.IsVisible)
            {
                Children.Add(sub);
            }
        }
        foreach (var item in Items)
        {
            if (item.IsVisible)
            {
                Children.Add(item);
            }
        }
    }

    public bool ApplyFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            IsVisible = true;
            foreach (var item in Items) item.IsVisible = true;
            foreach (var sub in Subgroups) sub.ApplyFilter(query);
            RefreshChildren();
            return true;
        }

        int matchingItems = 0;
        foreach (var item in Items)
        {
            bool matches = item.Matches(query);
            item.IsVisible = matches;
            if (matches) matchingItems++;
        }

        int matchingSubgroups = 0;
        foreach (var sub in Subgroups)
        {
            if (sub.ApplyFilter(query))
            {
                matchingSubgroups++;
            }
        }

        bool groupTitleMatches = Title.Contains(query, StringComparison.OrdinalIgnoreCase);
        bool hasMatches = matchingItems > 0 || matchingSubgroups > 0 || groupTitleMatches;

        IsVisible = hasMatches;
        if (hasMatches)
        {
            IsExpanded = true;
        }

        RefreshChildren();
        return hasMatches;
    }
}
