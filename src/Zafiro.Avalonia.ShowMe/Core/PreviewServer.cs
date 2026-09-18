using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Viewport;

namespace Zafiro.Avalonia.ShowMe.Core;

public sealed class PreviewServer : IDisposable
{
    private readonly PreviewTarget target;
    private readonly BsonTcpTransport transport;
    private IDisposable? listener;
    private Process? process;
    private IAvaloniaRemoteTransportConnection? connection;
    private readonly object syncLock = new();

    private double currentWidth = 1024;
    private double currentHeight = 768;
    private double currentDpi = 96.0;
    private string currentTheme = XamlThemeModifier.ThemeDefault;
    private string lastRawXaml = "";
    private bool isDisposed;

    public event Action<FrameMessage>? FrameReceived;
    public event Action<UpdateXamlResultMessage>? XamlResultReceived;
    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;

    public PreviewServer(PreviewTarget target, int initialWidth = 1024, int initialHeight = 768)
    {
        this.target = target;
        currentWidth = target.InitialWidth ?? initialWidth;
        currentHeight = target.InitialHeight ?? initialHeight;
        transport = new BsonTcpTransport();
    }

    public async Task StartAsync(string initialXaml, string theme = XamlThemeModifier.ThemeDefault, CancellationToken cancellationToken = default)
    {
        lastRawXaml = initialXaml;
        currentTheme = theme;

        // 1. Asignar puerto libre
        var port = GetFreePort();

        StatusChanged?.Invoke($"Iniciando escucha en puerto {port}...");

        // 2. Iniciar servidor BSON TCP
        listener = transport.Listen(IPAddress.Loopback, port, OnClientConnected);

        // 3. Iniciar proceso del previewer oficial de Avalonia
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "exec",
                "--runtimeconfig", target.RuntimeConfigPath,
                "--depsfile", target.DepsJsonPath,
                target.DesignerHostPath,
                "--transport", $"tcp-bson://127.0.0.1:{port}/",
                "--session-id", Guid.NewGuid().ToString(),
                "--method", "avalonia-remote",
                target.TargetAssemblyPath
            },
            WorkingDirectory = target.TargetDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        StatusChanged?.Invoke("Lanzando proceso del Avalonia Previewer...");

        process = Process.Start(psi);
        if (process == null)
        {
            ErrorOccurred?.Invoke("No se pudo iniciar el proceso dotnet para el previewer.");
            return;
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Trace.WriteLine($"[Designer-Out] {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Trace.WriteLine($"[Designer-Err] {e.Data}");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private void OnClientConnected(IAvaloniaRemoteTransportConnection conn)
    {
        lock (syncLock)
        {
            connection = conn;
        }

        StatusChanged?.Invoke("Previewer conectado. Configurando sesión...");

        conn.OnMessage += OnTransportMessage;
        conn.OnException += (c, ex) =>
        {
            ErrorOccurred?.Invoke($"Error en transporte del previewer: {ex.Message}");
        };
    }

    private void OnTransportMessage(IAvaloniaRemoteTransportConnection conn, object message)
    {
        if (message is StartDesignerSessionMessage session)
        {
            StatusChanged?.Invoke("Sesión de diseñador inicializada.");

            conn.Send(new ClientSupportedPixelFormatsMessage
            {
                Formats = [PixelFormat.Bgra8888, PixelFormat.Rgba8888]
            });

            conn.Send(new ClientRenderInfoMessage
            {
                DpiX = currentDpi,
                DpiY = currentDpi
            });

            conn.Send(new ClientViewportAllocatedMessage
            {
                Width = currentWidth,
                Height = currentHeight,
                DpiX = currentDpi,
                DpiY = currentDpi
            });

            SendCurrentXaml();
        }
        else if (message is UpdateXamlResultMessage result)
        {
            XamlResultReceived?.Invoke(result);
            if (string.IsNullOrEmpty(result.Error))
            {
                StatusChanged?.Invoke("XAML previsualizado correctamente.");
            }
            else
            {
                StatusChanged?.Invoke($"Error en XAML: {result.Error}");
            }
        }
        else if (message is FrameMessage frame)
        {
            FrameReceived?.Invoke(frame);
            conn.Send(new FrameReceivedMessage { SequenceId = frame.SequenceId });
        }
        else if (message is RequestViewportResizeMessage resize)
        {
            // El diseñador indica el tamaño deseado por el control
            if (resize.Width > 0 && resize.Height > 0)
            {
                currentWidth = resize.Width;
                currentHeight = resize.Height;
                conn.Send(new ClientViewportAllocatedMessage
                {
                    Width = currentWidth,
                    Height = currentHeight,
                    DpiX = currentDpi,
                    DpiY = currentDpi
                });
            }
        }
    }

    public void UpdateXaml(string rawXaml, string? theme = null)
    {
        lastRawXaml = rawXaml;
        if (theme != null)
        {
            currentTheme = theme;
        }

        SendCurrentXaml();
    }

    public void UpdateTheme(string theme)
    {
        currentTheme = theme;
        SendCurrentXaml();
    }

    public void SetViewportSize(double width, double height, double dpi = 96.0)
    {
        currentWidth = width;
        currentHeight = height;
        currentDpi = dpi;

        lock (syncLock)
        {
            connection?.Send(new ClientViewportAllocatedMessage
            {
                Width = currentWidth,
                Height = currentHeight,
                DpiX = currentDpi,
                DpiY = currentDpi
            });
        }
    }

    private void SendCurrentXaml()
    {
        lock (syncLock)
        {
            if (connection == null)
            {
                return;
            }

            var processedXaml = XamlThemeModifier.ApplyTheme(lastRawXaml, currentTheme);

            connection.Send(new UpdateXamlMessage
            {
                Xaml = processedXaml,
                AssemblyPath = target.TargetAssemblyPath,
                XamlFileProjectPath = target.RelativeXamlPath
            });
        }
    }

    private static int GetFreePort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        try
        {
            listener?.Dispose();
        }
        catch { }

        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
            }
        }
        catch { }
    }
}
