using System;
using System.IO;
using System.Linq;

namespace Zafiro.Avalonia.ShowMe.Core;

public static class SourceFileResolver
{
    public static string Resolve(string? sourceUri, PreviewTarget target)
    {
        if (string.IsNullOrWhiteSpace(sourceUri))
        {
            return target.AxamlPath;
        }

        // Case 1: file:// URI
        if (Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                var localPath = uri.LocalPath;
                if (File.Exists(localPath))
                {
                    return localPath;
                }
            }
            else if (string.Equals(uri.Scheme, "avares", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolveAvaresUri(uri, target);
                if (resolved != null && File.Exists(resolved))
                {
                    return resolved;
                }
            }
        }
        else if (File.Exists(sourceUri))
        {
            return Path.GetFullPath(sourceUri);
        }

        // Case 2: Relative path string
        var normalized = sourceUri.TrimStart('/').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var containingDir = Path.GetDirectoryName(target.ContainingProjectPath);
        if (containingDir != null)
        {
            var candidate = Path.Combine(containingDir, normalized);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Case 3: Try within repository / solution root
        var repoRoot = FindRepositoryOrSolutionRoot(target);
        if (repoRoot != null)
        {
            var candidateInRepo = Path.Combine(repoRoot, normalized);
            if (File.Exists(candidateInRepo))
            {
                return candidateInRepo;
            }

            var fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(fileName))
            {
                var found = FindFileByName(repoRoot, fileName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return target.AxamlPath;
    }

    private static string? ResolveAvaresUri(Uri avaresUri, PreviewTarget target)
    {
        // Format: avares://AssemblyName/Path/To/View.axaml
        var assemblyName = avaresUri.Host;
        var relativePath = Uri.UnescapeDataString(avaresUri.AbsolutePath).TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        // 1. Check in target containing project directory
        var containingDir = Path.GetDirectoryName(target.ContainingProjectPath);
        if (containingDir != null)
        {
            var candidate = Path.Combine(containingDir, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 2. Check solution / repo root
        var repoRoot = FindRepositoryOrSolutionRoot(target);
        if (repoRoot != null)
        {
            // Direct candidate: {repoRoot}/{assemblyName}/{relativePath}
            var direct = Path.Combine(repoRoot, assemblyName, relativePath);
            if (File.Exists(direct))
            {
                return direct;
            }

            // Direct candidate in src/: {repoRoot}/src/{assemblyName}/{relativePath}
            var inSrc = Path.Combine(repoRoot, "src", assemblyName, relativePath);
            if (File.Exists(inSrc))
            {
                return inSrc;
            }

            // Direct candidate in source/: {repoRoot}/source/{assemblyName}/{relativePath}
            var inSource = Path.Combine(repoRoot, "source", assemblyName, relativePath);
            if (File.Exists(inSource))
            {
                return inSource;
            }

            // Look for any project directory matching assemblyName
            try
            {
                var candidateDirs = Directory.GetDirectories(repoRoot, assemblyName, SearchOption.AllDirectories)
                    .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                                !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                                !d.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"));

                foreach (var dir in candidateDirs)
                {
                    var file = Path.Combine(dir, relativePath);
                    if (File.Exists(file))
                    {
                        return file;
                    }
                }
            }
            catch { }

            // Search by filename inside the repo (excluding bin, obj, .git)
            var fileName = Path.GetFileName(relativePath);
            if (!string.IsNullOrEmpty(fileName))
            {
                var found = FindFileByName(repoRoot, fileName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public static string? FindRepositoryOrSolutionRoot(PreviewTarget target)
    {
        var startDir = Path.GetDirectoryName(target.ContainingProjectPath)
                    ?? Path.GetDirectoryName(target.HostProjectPath)
                    ?? Path.GetDirectoryName(target.AxamlPath);

        if (string.IsNullOrEmpty(startDir) || !Directory.Exists(startDir)) return null;

        try
        {
            var current = new DirectoryInfo(startDir);
            DirectoryInfo? lastWithProjects = null;

            while (current != null && current.Exists)
            {
                if (current.GetFiles("*.sln").Length > 0 ||
                    current.GetFiles("*.slnx").Length > 0 ||
                    current.GetDirectories(".git").Length > 0)
                {
                    return current.FullName;
                }

                if (current.GetFiles("*.csproj").Length > 0)
                {
                    lastWithProjects = current;
                }

                current = current.Parent;
            }

            return lastWithProjects?.Parent?.FullName ?? startDir;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindFileByName(string rootDir, string fileName)
    {
        if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir)) return null;

        try
        {
            foreach (var file in Directory.EnumerateFiles(rootDir, fileName, SearchOption.AllDirectories))
            {
                var normalized = file.Replace('\\', '/');
                if (!normalized.Contains("/bin/") &&
                    !normalized.Contains("/obj/") &&
                    !normalized.Contains("/.git/"))
                {
                    return file;
                }
            }
        }
        catch { }

        return null;
    }
}
