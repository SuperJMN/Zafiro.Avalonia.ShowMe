using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using CSharpFunctionalExtensions;

namespace Zafiro.Avalonia.ShowMe.Core;

public sealed class PreviewTargetResolver
{
    public static async Task<Result<PreviewTarget>> ResolveAsync(
        string axamlPath,
        string? explicitProjectPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullAxamlPath = Path.GetFullPath(CommandLineArgs.NormalizeFilePath(axamlPath));
            if (!File.Exists(fullAxamlPath))
            {
                return Result.Failure<PreviewTarget>($"El archivo AXAML no existe: '{fullAxamlPath}'");
            }

            // 1. Localizar el proyecto contenedor
            var containingProjectResult = FindContainingProject(fullAxamlPath);
            if (containingProjectResult.IsFailure)
            {
                return Result.Failure<PreviewTarget>(containingProjectResult.Error);
            }
            var containingProjectPath = containingProjectResult.Value;

            // 2. Determinar el proyecto host (ejecutable)
            string hostProjectPath;
            if (!string.IsNullOrWhiteSpace(explicitProjectPath))
            {
                hostProjectPath = Path.GetFullPath(CommandLineArgs.NormalizeFilePath(explicitProjectPath));
                if (!File.Exists(hostProjectPath))
                {
                    return Result.Failure<PreviewTarget>($"El proyecto host especificado no existe: '{hostProjectPath}'");
                }
            }
            else
            {
                var hostResult = FindHostProject(containingProjectPath);
                hostProjectPath = hostResult.GetValueOrDefault(containingProjectPath);
            }

            // 3. Evaluar propiedades MSBuild
            var msbuildResult = await EvaluateMsBuildPropertiesAsync(hostProjectPath, cancellationToken);
            if (msbuildResult.IsFailure)
            {
                return Result.Failure<PreviewTarget>(msbuildResult.Error);
            }
            var props = msbuildResult.Value;

            var targetPath = props.TargetPath;
            var targetDir = props.TargetDir;
            var targetName = props.TargetName;
            var projectDir = props.ProjectDir;

            // 4. Si el binario no existe, compilar el proyecto
            if (!File.Exists(targetPath))
            {
                var buildResult = await BuildProjectAsync(hostProjectPath, cancellationToken);
                if (buildResult.IsFailure)
                {
                    return Result.Failure<PreviewTarget>(buildResult.Error);
                }

                if (!File.Exists(targetPath))
                {
                    return Result.Failure<PreviewTarget>($"La compilación de '{hostProjectPath}' no generó el binario en '{targetPath}'.");
                }
            }

            // 5. Localizar Avalonia.Designer.HostApp.dll
            var designerHostPath = props.DesignerToolPath;
            if (string.IsNullOrWhiteSpace(designerHostPath) || !File.Exists(designerHostPath))
            {
                designerHostPath = FindFallbackDesignerHostApp(targetDir);
            }

            if (string.IsNullOrWhiteSpace(designerHostPath) || !File.Exists(designerHostPath))
            {
                return Result.Failure<PreviewTarget>("No se pudo localizar 'Avalonia.Designer.HostApp.dll'. Asegúrate de que el proyecto tenga referencia a Avalonia.");
            }

            // 6. Localizar runtimeconfig.json y deps.json
            var runtimeConfigPath = Path.Combine(targetDir, $"{targetName}.runtimeconfig.json");
            var depsJsonPath = Path.Combine(targetDir, $"{targetName}.deps.json");

            if (!File.Exists(runtimeConfigPath))
            {
                // Intentar encontrar cualquier runtimeconfig.json en targetDir
                var candidate = Directory.GetFiles(targetDir, "*.runtimeconfig.json").FirstOrDefault();
                if (candidate != null)
                {
                    runtimeConfigPath = candidate;
                }
            }

            if (!File.Exists(depsJsonPath))
            {
                var candidate = Directory.GetFiles(targetDir, "*.deps.json").FirstOrDefault();
                if (candidate != null)
                {
                    depsJsonPath = candidate;
                }
            }

            // 7. Calcular ruta relativa del XAML (relativa a su proyecto contenedor)
            var containingProjectDir = Path.GetDirectoryName(containingProjectPath) ?? projectDir;
            var relativeXamlPath = Path.GetRelativePath(containingProjectDir, fullAxamlPath)
                .Replace('\\', '/');
            if (!relativeXamlPath.StartsWith('/'))
            {
                relativeXamlPath = "/" + relativeXamlPath;
            }

            // Determinar el ensamblado que contiene el XAML
            var containingProjectName = Path.GetFileNameWithoutExtension(containingProjectPath);
            var candidateXamlAssembly = Path.Combine(targetDir, containingProjectName + ".dll");
            var xamlAssemblyPath = File.Exists(candidateXamlAssembly) ? candidateXamlAssembly : targetPath;

            // 8. Extraer dimensiones de diseño del XAML si existen
            var (initialWidth, initialHeight) = ExtractDesignDimensions(fullAxamlPath);

            var target = new PreviewTarget(
                fullAxamlPath,
                containingProjectPath,
                hostProjectPath,
                targetPath,
                xamlAssemblyPath,
                targetDir,
                targetName,
                designerHostPath,
                runtimeConfigPath,
                depsJsonPath,
                relativeXamlPath,
                initialWidth,
                initialHeight);

            return Result.Success(target);
        }
        catch (Exception ex)
        {
            return Result.Failure<PreviewTarget>($"Error al resolver el contexto de la aplicación: {ex.Message}");
        }
    }

    private static Result<string> FindContainingProject(string axamlPath)
    {
        var currentDir = Path.GetDirectoryName(axamlPath);
        while (!string.IsNullOrEmpty(currentDir))
        {
            var projectFiles = Directory.GetFiles(currentDir, "*.csproj");
            if (projectFiles.Length > 0)
            {
                return Result.Success(projectFiles[0]);
            }

            currentDir = Path.GetDirectoryName(currentDir);
        }

        return Result.Failure<string>($"No se encontró ningún archivo .csproj en los directorios superiores a '{axamlPath}'.");
    }

    private static Maybe<string> FindHostProject(string containingProjectPath)
    {
        try
        {
            // Si el proyecto contenedor ya es ejecutable, usarlo directamente
            if (IsExecutableProject(containingProjectPath))
            {
                return Maybe<string>.From(containingProjectPath);
            }

            // Buscar en el directorio de la solución o raíz del repositorio
            var rootDir = FindSolutionOrRepoRoot(containingProjectPath);
            var candidateProjects = Directory.GetFiles(rootDir, "*.csproj", SearchOption.AllDirectories);

            var containingName = Path.GetFileNameWithoutExtension(containingProjectPath);

            // Filtrar proyectos ejecutables que referencien al proyecto contenedor
            var executables = new List<string>();
            foreach (var proj in candidateProjects)
            {
                if (IsExecutableProject(proj))
                {
                    executables.Add(proj);
                }
            }

            // Buscar uno que haga referencia directa al proyecto contenedor
            var referencingExecutables = executables.Where(e =>
            {
                var content = File.ReadAllText(e);
                return content.Contains(Path.GetFileName(containingProjectPath), StringComparison.OrdinalIgnoreCase) ||
                       content.Contains(containingName, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (referencingExecutables.Count > 0)
            {
                // Preferir proyectos Desktop
                var desktopHost = referencingExecutables.FirstOrDefault(p =>
                    p.Contains(".Desktop", StringComparison.OrdinalIgnoreCase));
                return Maybe<string>.From(desktopHost ?? referencingExecutables[0]);
            }

            // Si ningún ejecutable lo referencia explícitamente, pero hay proyectos .Desktop
            var desktopProjects = executables.Where(p =>
                p.Contains(".Desktop", StringComparison.OrdinalIgnoreCase)).ToList();
            if (desktopProjects.Count > 0)
            {
                return Maybe<string>.From(desktopProjects[0]);
            }

            if (executables.Count > 0)
            {
                return Maybe<string>.From(executables[0]);
            }
        }
        catch
        {
            // Ignorar y retornar Maybe.None
        }

        return Maybe<string>.None;
    }

    private static bool IsExecutableProject(string csprojPath)
    {
        try
        {
            var content = File.ReadAllText(csprojPath);
            return content.Contains("<OutputType>WinExe</OutputType>", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string FindSolutionOrRepoRoot(string projectPath)
    {
        var current = Path.GetDirectoryName(projectPath);
        string lastFound = current ?? Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "*.sln").Length > 0 ||
                Directory.GetFiles(current, "*.slnx").Length > 0 ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            lastFound = current;
            current = Path.GetDirectoryName(current);
        }

        return lastFound;
    }

    private static async Task<Result<MsBuildProperties>> EvaluateMsBuildPropertiesAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "msbuild",
                projectPath,
                "-nologo",
                "-getProperty:AvaloniaPreviewerNetCoreToolPath",
                "-getProperty:TargetPath",
                "-getProperty:TargetDir",
                "-getProperty:TargetName",
                "-getProperty:ProjectDir"
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return Result.Failure<MsBuildProperties>("No se pudo iniciar el proceso dotnet msbuild.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            return Result.Failure<MsBuildProperties>($"dotnet msbuild falló con código {process.ExitCode}: {stderr}\n{stdout}");
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            var props = root.GetProperty("Properties");

            var targetPath = props.GetProperty("TargetPath").GetString() ?? "";
            var targetDir = props.GetProperty("TargetDir").GetString() ?? "";
            var targetName = props.GetProperty("TargetName").GetString() ?? "";
            var projectDir = props.GetProperty("ProjectDir").GetString() ?? "";
            var designerToolPath = props.TryGetProperty("AvaloniaPreviewerNetCoreToolPath", out var toolProp)
                ? toolProp.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return Result.Failure<MsBuildProperties>("MSBuild no devolvió TargetPath.");
            }

            return Result.Success(new MsBuildProperties(targetPath, targetDir, targetName, projectDir, designerToolPath));
        }
        catch (Exception ex)
        {
            return Result.Failure<MsBuildProperties>($"Error al analizar la salida de MSBuild: {ex.Message}\nSalida: {stdout}");
        }
    }

    private static async Task<Result> BuildProjectAsync(string projectPath, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "build",
                projectPath,
                "-c", "Debug",
                "--nologo",
                "-v", "minimal"
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return Result.Failure("No se pudo iniciar el proceso de compilación.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            return Result.Failure($"Error de compilación ({process.ExitCode}):\n{stderr}\n{stdout}");
        }

        return Result.Success();
    }

    private static string FindFallbackDesignerHostApp(string targetDir)
    {
        // 1. Comprobar en targetDir o subdirectorios
        var candidateInTarget = Path.Combine(targetDir, "Avalonia.Designer.HostApp.dll");
        if (File.Exists(candidateInTarget))
        {
            return candidateInTarget;
        }

        // 2. Buscar en cache de nuget ~/.nuget/packages/avalonia/
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nugetPath = Path.Combine(userProfile, ".nuget", "packages", "avalonia");
        if (Directory.Exists(nugetPath))
        {
            var versions = Directory.GetDirectories(nugetPath)
                .OrderByDescending(d => d)
                .ToList();

            foreach (var versionDir in versions)
            {
                var matches = Directory.GetFiles(versionDir, "Avalonia.Designer.HostApp.dll", SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    // Preferir net8.0 o net10.0 o netstandard2.0
                    var preferred = matches.FirstOrDefault(m => m.Contains("net8.0") || m.Contains("net10.0"))
                                   ?? matches[0];
                    return preferred;
                }
            }
        }

        return "";
    }

    private static (int? Width, int? Height) ExtractDesignDimensions(string axamlPath)
    {
        try
        {
            var doc = XDocument.Load(axamlPath, LoadOptions.None);
            var root = doc.Root;
            if (root == null) return (null, null);

            int? width = null;
            int? height = null;

            // d:DesignWidth o Width
            var dWidth = root.Attributes().FirstOrDefault(a => a.Name.LocalName == "DesignWidth");
            var dHeight = root.Attributes().FirstOrDefault(a => a.Name.LocalName == "DesignHeight");

            if (dWidth != null && double.TryParse(dWidth.Value, out var dw))
            {
                width = (int)Math.Round(dw);
            }
            else
            {
                var widthAttr = root.Attribute("Width");
                if (widthAttr != null && double.TryParse(widthAttr.Value, out var w))
                {
                    width = (int)Math.Round(w);
                }
            }

            if (dHeight != null && double.TryParse(dHeight.Value, out var dh))
            {
                height = (int)Math.Round(dh);
            }
            else
            {
                var heightAttr = root.Attribute("Height");
                if (heightAttr != null && double.TryParse(heightAttr.Value, out var h))
                {
                    height = (int)Math.Round(h);
                }
            }

            return (width, height);
        }
        catch
        {
            return (null, null);
        }
    }

    private sealed record MsBuildProperties(
        string TargetPath,
        string TargetDir,
        string TargetName,
        string ProjectDir,
        string DesignerToolPath);
}
