using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Zafiro.Avalonia.ShowMe.Host;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}

public class Program
{
    public static int Main(string[] args)
    {
        SetupAssemblyResolver();
        return Run(args);
    }

    public static string? TargetDirectory { get; set; }

    private static void SetupAssemblyResolver()
    {
        var hostDir = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        Assembly? ResolveCore(AssemblyName assemblyName, AssemblyLoadContext? context)
        {
            context ??= AssemblyLoadContext.Default;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return asm;
                }
            }

            var dir = TargetDirectory ?? currentDir;

            // Mapeos de alias conocidos (compatibilidad con librerías de behaviors renombradas)
            if (string.Equals(assemblyName.Name, "Avalonia.Xaml.Interactivity", StringComparison.OrdinalIgnoreCase))
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "Xaml.Behaviors.Interactivity", StringComparison.OrdinalIgnoreCase));
                if (loaded != null) return loaded;

                var candidate = Path.Combine(dir, "Xaml.Behaviors.Interactivity.dll");
                if (File.Exists(candidate))
                {
                    try { return context.LoadFromAssemblyPath(candidate); } catch { }
                }
            }
            if (string.Equals(assemblyName.Name, "Avalonia.Xaml.Interactions", StringComparison.OrdinalIgnoreCase))
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "Xaml.Behaviors.Interactions", StringComparison.OrdinalIgnoreCase));
                if (loaded != null) return loaded;

                var candidate = Path.Combine(dir, "Xaml.Behaviors.Interactions.dll");
                if (File.Exists(candidate))
                {
                    try { return context.LoadFromAssemblyPath(candidate); } catch { }
                }
            }

            // 1. Preferir ensamblados del host (Avalonia.*, ShowMe.Protocol, etc.) para mantener versiones unificadas
            var hostCandidate = Path.Combine(hostDir, assemblyName.Name + ".dll");
            if (File.Exists(hostCandidate))
            {
                try
                {
                    return context.LoadFromAssemblyPath(hostCandidate);
                }
                catch { }
            }

            // 2. Para ensamblados del proyecto destino (SampleApp.dll, librerías del usuario, etc.)
            var targetCandidate = Path.Combine(dir, assemblyName.Name + ".dll");
            if (File.Exists(targetCandidate))
            {
                try
                {
                    return context.LoadFromAssemblyPath(targetCandidate);
                }
                catch { }
            }

            return null;
        }

        AssemblyLoadContext.Default.Resolving += (context, assemblyName) => ResolveCore(assemblyName, context);
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ResolveCore(new AssemblyName(args.Name), null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        int port = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedPort))
            {
                port = parsedPort;
                i++;
            }
        }

        if (port <= 0)
        {
            Console.Error.WriteLine("Error: Se requiere el argumento --port <número_puerto>.");
            return 1;
        }

        try
        {
            // Inicializar el entorno Avalonia Headless con renderizado Skia real en el hilo principal
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .WithInterFont()
                .SetupWithoutStarting();

            var cts = new CancellationTokenSource();

            // Ejecutar la conexión y protocolo en segundo plano
            _ = Task.Run(async () =>
            {
                try
                {
                    using var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);

                    var service = new ShowMeHostService(tcpClient);
                    await service.RunAsync(cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ShowMe.Host] Error en servicio de host: {ex.Message}");
                }
                finally
                {
                    cts.Cancel();
                }
            });

            // El hilo principal ejecuta el bucle de despacho de Avalonia (UI Thread)
            Dispatcher.UIThread.MainLoop(cts.Token);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ShowMe.Host] Error fatal: {ex.Message}");
            return 2;
        }
    }
}
