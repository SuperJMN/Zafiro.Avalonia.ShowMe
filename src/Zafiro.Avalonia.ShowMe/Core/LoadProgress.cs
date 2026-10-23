namespace Zafiro.Avalonia.ShowMe.Core;

public enum LoadStep
{
    FindingProject,
    EvaluatingMsBuild,
    Building,
    StartingPreviewer,
    Rendering
}

public sealed record LoadProgress(LoadStep Step, string Title, string? Detail = null);
