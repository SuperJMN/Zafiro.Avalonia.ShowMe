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

    private static void SetupAssemblyResolver()
    {
        var hostDir = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? AppContext.BaseDirectory;
        var currentDir = Directory.GetCurrentDirectory();

        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return asm;
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
            var targetCandidate = Path.Combine(currentDir, assemblyName.Name + ".dll");
            if (File.Exists(targetCandidate))
            {
                try
                {
                    return context.LoadFromAssemblyPath(targetCandidate);
                }
                catch { }
            }

            return null;
        };
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
