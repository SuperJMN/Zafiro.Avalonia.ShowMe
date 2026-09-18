namespace Zafiro.Avalonia.ShowMe.Core;

public sealed record CommandLineArgs(
    string? FilePath = null,
    string? ProjectPath = null,
    string? Theme = null,
    int? Width = null,
    int? Height = null,
    bool ShowHelp = false)
{
    public static CommandLineArgs Parse(string[] args)
    {
        string? filePath = null;
        string? projectPath = null;
        string? theme = null;
        int? width = null;
        int? height = null;
        bool showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg is "--help" or "-h" or "-?" or "/?")
            {
                showHelp = true;
                break;
            }

            if (arg is "--project" or "-p" && i + 1 < args.Length)
            {
                projectPath = args[++i];
            }
            else if (arg is "--theme" or "-t" && i + 1 < args.Length)
            {
                theme = args[++i];
            }
            else if (arg is "--width" or "-w" && i + 1 < args.Length && int.TryParse(args[++i], out var w))
            {
                width = w;
            }
            else if (arg is "--height" && i + 1 < args.Length && int.TryParse(args[++i], out var h))
            {
                height = h;
            }
            else if (!arg.StartsWith('-') && filePath == null)
            {
                filePath = arg;
            }
        }

        return new CommandLineArgs(filePath, projectPath, theme, width, height, showHelp);
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            Zafiro.Avalonia.ShowMe - Avalonia XAML Previewer
            
            Uso:
              zafiro-avalonia-showme [ruta-al-archivo-axaml] [opciones]
            
            Argumentos:
              ruta-al-archivo-axaml   Ruta al archivo .axaml o .xaml a previsualizar.
            
            Opciones:
              -p, --project <ruta>    Ruta explícita al proyecto .csproj de la aplicación host.
              -t, --theme <tema>      Tema inicial: 'Default', 'Light' o 'Dark'.
              -w, --width <pixels>    Ancho inicial del viewport en píxeles.
              -h, --height <pixels>   Alto inicial del viewport en píxeles.
              -?, -h, --help          Muestra esta ayuda.
            
            Ejemplos:
              zafiro-avalonia-showme src/MyApp/Views/MainView.axaml
              zafiro-avalonia-showme src/MyApp/Views/DetailView.axaml --theme Dark
              zafiro-avalonia-showme MainWindow.axaml -w 1280 -h 720
            """);
    }
}
