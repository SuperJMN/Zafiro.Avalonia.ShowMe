namespace Zafiro.Avalonia.ShowMe.ViewModels;

public sealed record DimensionPreset(string Name, double Width, double Height)
{
    public string DisplayText => $"{Name} ({Width:F0} × {Height:F0})";

    public override string ToString() => DisplayText;
}
