namespace Zafiro.Avalonia.ShowMe.Core;

public sealed record PreviewTarget(
    string AxamlPath,
    string ContainingProjectPath,
    string HostProjectPath,
    string TargetAssemblyPath,
    string XamlAssemblyPath,
    string TargetDirectory,
    string TargetName,
    string DesignerHostPath,
    string RuntimeConfigPath,
    string DepsJsonPath,
    string RelativeXamlPath,
    int? InitialWidth,
    int? InitialHeight);
