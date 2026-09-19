using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Zafiro.Avalonia.ShowMe.Protocol;

namespace Zafiro.Avalonia.ShowMe.Core;

public sealed class PreviewServer : IDisposable
{
    private readonly PreviewTarget target;
    private TcpListener? listener;
    private TcpClient? client;
    private Stream? stream;
    private Process? process;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly CancellationTokenSource cts = new();

    private double currentWidth = 1024;
    private double currentHeight = 768;
    private double currentDpi = 96.0;
    private string currentTheme = XamlThemeModifier.ThemeDefault;
    private string lastRawXaml = "";
    private bool isDisposed;

    public event Action<ShowMeFramePacket>? FrameReceived;
    public event Action<XamlStatusMessage>? XamlStatusReceived;
    public event Action<HitTestResponseMessage>? HitTestResultReceived;
    public event Action<ContextMenuHitTestResponseMessage>? ContextMenuHitTestResultReceived;
    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;
    public event Action<string>? LogReceived;

    public PreviewServer(PreviewTarget target, int initialWidth = 1024, int initialHeight = 768)
    {
        this.target = target;
        currentWidth = target.InitialWidth ?? initialWidth;
        currentHeight = target.InitialHeight ?? initialHeight;
    }

    public async Task StartAsync(string initialXaml, string theme = XamlThemeModifier.ThemeDefault, CancellationToken cancellationToken = default)
    {
        lastRawXaml = initialXaml;
        currentTheme = theme;

        // 1. Asignar puerto libre e iniciar listener TCP
        var port = GetFreePort();
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        StatusChanged?.Invoke($"Iniciando escucha en puerto {port}...");

        // 2. Iniciar proceso ShowMe.Host
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                target.DesignerHostPath,
                "--port", port.ToString()
            },
            WorkingDirectory = target.TargetDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        StatusChanged?.Invoke("Lanzando proceso ShowMe.Host...");

        process = Process.Start(psi);
        if (process == null)
        {
            ErrorOccurred?.Invoke("No se pudo iniciar el proceso dotnet para ShowMe.Host.");
            return;
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                LogReceived?.Invoke($"[Host-Out] {e.Data}");
                Trace.WriteLine($"[Host-Out] {e.Data}");
            }
        };

        var stderrBuilder = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stderrBuilder.AppendLine(e.Data);
                LogReceived?.Invoke($"[Host-Err] {e.Data}");
                Trace.WriteLine($"[Host-Err] {e.Data}");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 3. Esperar conexión del Host
        StatusChanged?.Invoke("Esperando conexión de ShowMe.Host...");
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        var acceptTask = listener.AcceptTcpClientAsync(linkedCts.Token).AsTask();
        var exitTask = process.WaitForExitAsync(linkedCts.Token);
        var timeoutTask = Task.Delay(12000, linkedCts.Token);

        var completed = await Task.WhenAny(acceptTask, exitTask, timeoutTask).ConfigureAwait(false);
        if (completed == exitTask)
        {
            var err = stderrBuilder.ToString();
            throw new InvalidOperationException($"El proceso ShowMe.Host terminó inesperadamente con código {process.ExitCode}.\n{err}");
        }
        if (completed == timeoutTask)
        {
            var err = stderrBuilder.ToString();
            throw new TimeoutException($"Tiempo de espera agotado (12s) esperando conexión de ShowMe.Host.\n{err}");
        }

        client = await acceptTask.ConfigureAwait(false);
        stream = client.GetStream();

        StatusChanged?.Invoke("ShowMe.Host conectado. Inicializando vista...");

        // 4. Enviar mensaje de inicialización
        var init = new InitMessage(
            TargetAssemblyPath: target.TargetAssemblyPath,
            InitialXaml: lastRawXaml,
            Theme: currentTheme,
            Width: currentWidth,
            Height: currentHeight,
            Dpi: currentDpi,
            XamlAssemblyPath: target.XamlAssemblyPath
        );

        await SendMessageAsync(init, linkedCts.Token).ConfigureAwait(false);

        // 5. Iniciar loop de lectura de paquetes
        _ = Task.Run(ReadLoopAsync, cts.Token);
    }

    private async Task ReadLoopAsync()
    {
        if (stream == null) return;

        try
        {
            while (!cts.IsCancellationRequested && client != null && client.Connected)
            {
                var packet = await ShowMeFraming.ReadPacketAsync(stream, cts.Token).ConfigureAwait(false);
                if (packet == null)
                {
                    break;
                }

                if (packet is ShowMeFramePacket frame)
                {
                    FrameReceived?.Invoke(frame);
                }
                else if (packet is XamlStatusMessage status)
                {
                    XamlStatusReceived?.Invoke(status);
                    if (status.Success)
                    {
                        StatusChanged?.Invoke("XAML cargado e instanciado correctamente.");
                    }
                    else
                    {
                        StatusChanged?.Invoke($"Error en XAML: {status.Error}");
                    }
                }
                else if (packet is HitTestResponseMessage hit)
                {
                    HitTestResultReceived?.Invoke(hit);
                }
                else if (packet is ContextMenuHitTestResponseMessage ctxHit)
                {
                    ContextMenuHitTestResultReceived?.Invoke(ctxHit);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!isDisposed)
            {
                ErrorOccurred?.Invoke($"Error de comunicación con ShowMe.Host: {ex.Message}");
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

        _ = SendMessageAsync(new UpdateXamlMessage(rawXaml, currentTheme), cts.Token);
    }

    public void UpdateTheme(string theme)
    {
        currentTheme = theme;
        _ = SendMessageAsync(new UpdateXamlMessage(lastRawXaml, currentTheme), cts.Token);
    }

    public void SetViewportSize(double width, double height, double dpi = 96.0)
    {
        currentWidth = width;
        currentHeight = height;
        currentDpi = dpi;

        _ = SendMessageAsync(new ResizeViewportMessage(width, height, dpi), cts.Token);
    }

    public void ResetViewportSize(double width, double height)
    {
        SetViewportSize(width, height, currentDpi);
    }

    public void SendPointerEvent(
        PointerActionType action,
        double x,
        double y,
        PointerMouseButton button = PointerMouseButton.None,
        double deltaX = 0,
        double deltaY = 0,
        bool alt = false,
        bool ctrl = false,
        bool shift = false)
    {
        _ = SendMessageAsync(new PointerInputMessage(action, x, y, button, deltaX, deltaY, alt, ctrl, shift), cts.Token);
    }

    public void SendKeyEvent(
        KeyActionType action,
        int keyCode = 0,
        string? text = null,
        bool alt = false,
        bool ctrl = false,
        bool shift = false)
    {
        _ = SendMessageAsync(new KeyInputMessage(action, keyCode, text, alt, ctrl, shift), cts.Token);
    }

    public void RequestHitTest(double x, double y, bool isHover = false)
    {
        var reqId = Guid.NewGuid().ToString();
        _ = SendMessageAsync(new HitTestRequestMessage(reqId, x, y, isHover), cts.Token);
    }

    public void RequestContextMenuHitTest(double x, double y)
    {
        var reqId = Guid.NewGuid().ToString();
        _ = SendMessageAsync(new ContextMenuHitTestRequestMessage(reqId, x, y), cts.Token);
    }

    private async Task SendMessageAsync(ShowMeMessage msg, CancellationToken ct)
    {
        if (stream == null || client == null || !client.Connected) return;

        try
        {
            await sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await ShowMeFraming.WriteControlMessageAsync(stream, msg, ct).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }
        catch (Exception ex)
        {
            if (!isDisposed)
            {
                Trace.WriteLine($"[PreviewServer] Error enviando mensaje {msg.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static int GetFreePort()
    {
        using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)sock.LocalEndPoint!).Port;
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        cts.Cancel();
        cts.Dispose();

        try
        {
            stream?.Dispose();
            client?.Dispose();
            listener?.Stop();
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

        sendLock.Dispose();
    }
}
