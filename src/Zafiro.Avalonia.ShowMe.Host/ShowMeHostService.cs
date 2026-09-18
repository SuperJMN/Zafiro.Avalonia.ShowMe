using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.ShowMe.Protocol;

namespace Zafiro.Avalonia.ShowMe.Host;

public sealed class ShowMeHostService
{
    private readonly TcpClient client;
    private readonly Stream stream;
    private Window? window;
    private Assembly? targetAssembly;
    private XamlLineMapper? currentLineMapper;
    private double currentWidth = 800;
    private double currentHeight = 600;
    private long lastMoveFrameTime = System.Diagnostics.Stopwatch.GetTimestamp();
    private readonly SemaphoreSlim sendLock = new(1, 1);

    public ShowMeHostService(TcpClient client)
    {
        this.client = client;
        stream = client.GetStream();
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window = new Window
            {
                Width = currentWidth,
                Height = currentHeight,
                ShowInTaskbar = false
            };
            window.Show();
        });

        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                var packet = await ShowMeFraming.ReadPacketAsync(stream, ct).ConfigureAwait(false);
                if (packet == null)
                {
                    break;
                }

                if (packet is ShowMeMessage msg)
                {
                    await HandleMessageAsync(msg, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host-Service] Excepción en loop de lectura: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                window?.Close();
            });
        }
    }

    private async Task HandleMessageAsync(ShowMeMessage msg, CancellationToken ct)
    {
        switch (msg)
        {
            case InitMessage init:
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    currentWidth = init.Width;
                    currentHeight = init.Height;
                    if (window != null)
                    {
                        window.Width = currentWidth;
                        window.Height = currentHeight;
                    }

                    if (!string.IsNullOrEmpty(init.TargetAssemblyPath) && File.Exists(init.TargetAssemblyPath))
                    {
                        try
                        {
                            targetAssembly = Assembly.LoadFrom(init.TargetAssemblyPath);
                            TryLoadTargetAppResources(targetAssembly);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Host] Error cargando ensamblado destino: {ex.Message}");
                        }
                    }

                    LoadXaml(init.InitialXaml, init.Theme);
                });
                break;

            case UpdateXamlMessage update:
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LoadXaml(update.Xaml, update.Theme);
                });
                break;

            case ResizeViewportMessage resize:
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    currentWidth = resize.Width;
                    currentHeight = resize.Height;
                    if (window != null)
                    {
                        window.Width = currentWidth;
                        window.Height = currentHeight;
                        window.InvalidateMeasure();
                        window.UpdateLayout();
                    }
                    RenderAndSendFrame();
                });
                break;

            case PointerInputMessage pointer:
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (window == null) return;

                    var pt = new Point(pointer.X, pointer.Y);
                    var modifiers = ConvertModifiers(pointer.Alt, pointer.Control, pointer.Shift);

                    bool shouldRender = true;
                    switch (pointer.Action)
                    {
                        case PointerActionType.Move:
                            window.MouseMove(pt, modifiers);
                            if (System.Diagnostics.Stopwatch.GetElapsedTime(lastMoveFrameTime).TotalMilliseconds >= 33)
                            {
                                lastMoveFrameTime = System.Diagnostics.Stopwatch.GetTimestamp();
                            }
                            else
                            {
                                shouldRender = false;
                            }
                            break;
                        case PointerActionType.Down:
                            var btn = ConvertButton(pointer.Button);
                            window.MouseDown(pt, btn, modifiers);
                            break;
                        case PointerActionType.Up:
                            var btnUp = ConvertButton(pointer.Button);
                            window.MouseUp(pt, btnUp, modifiers);
                            break;
                        case PointerActionType.Wheel:
                            window.MouseWheel(pt, new Vector(pointer.DeltaX, pointer.DeltaY), modifiers);
                            break;
                    }

                    if (shouldRender)
                    {
                        RenderAndSendFrame();
                    }
                });
                break;

            case KeyInputMessage key:
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (window == null) return;

                    var modifiers = ConvertModifiers(key.Alt, key.Control, key.Shift);

                    switch (key.Action)
                    {
                        case KeyActionType.Down:
                            window.KeyPress((Key)key.KeyCode, modifiers, PhysicalKey.None, null);
                            break;
                        case KeyActionType.Up:
                            window.KeyRelease((Key)key.KeyCode, modifiers, PhysicalKey.None, null);
                            break;
                        case KeyActionType.TextInput:
                            if (!string.IsNullOrEmpty(key.Text))
                            {
                                window.KeyTextInput(key.Text);
                            }
                            break;
                    }

                    RenderAndSendFrame();
                });
                break;

            case HitTestRequestMessage hit:
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await HandleHitTestAsync(hit, ct);
                });
                break;
        }
    }

    private void LoadXaml(string xaml, string? theme)
    {
        if (window == null) return;

        try
        {
            currentLineMapper = XamlLineMapper.Parse(xaml);

            // Cargar control mediante AvaloniaRuntimeXamlLoader
            var loaded = AvaloniaRuntimeXamlLoader.Load(xaml, targetAssembly, null, null, true);

            if (loaded is Window userWindow)
            {
                var content = userWindow.Content;
                userWindow.Content = null;
                window.Content = content;
                foreach (var res in userWindow.Resources)
                {
                    window.Resources[res.Key] = res.Value;
                }
                foreach (var style in userWindow.Styles)
                {
                    window.Styles.Add(style);
                }
            }
            else if (loaded is Control control)
            {
                window.Content = control;
            }
            else if (loaded != null)
            {
                window.Content = new ContentControl { Content = loaded };
            }

            window.InvalidateMeasure();
            window.UpdateLayout();

            SendStatus(true, null, null, null);
            RenderAndSendFrame();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Host] Error parseando XAML: {ex}");
            SendStatus(false, ex.Message, null, null);
        }
    }

    private void RenderAndSendFrame()
    {
        if (window == null) return;

        try
        {
            var frame = window.CaptureRenderedFrame();
            if (frame == null)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
                frame = window.CaptureRenderedFrame();
            }
            if (frame == null) return;

            using var fb = frame.Lock();
            var stride = fb.RowBytes;
            var byteCount = stride * frame.PixelSize.Height;
            var buffer = new byte[byteCount];
            Marshal.Copy(fb.Address, buffer, 0, byteCount);

            Task.Run(async () =>
            {
                await sendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await ShowMeFraming.WriteFrameAsync(stream, frame.PixelSize.Width, frame.PixelSize.Height, stride, buffer).ConfigureAwait(false);
                }
                finally
                {
                    sendLock.Release();
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host] Error en captura de frame: {ex.Message}");
        }
    }

    private async Task HandleHitTestAsync(HitTestRequestMessage hit, CancellationToken ct)
    {
        if (window == null) return;

        var pt = new Point(hit.X, hit.Y);
        var inputElement = window.InputHitTest(pt);

        if (inputElement is Control control && control != window)
        {
            int line = 0;
            int col = 0;
            string? sourceUri = null;

            // Search up the visual tree for the element matching user XAML
            Control? inspected = control;
            while (inspected != null && inspected != window)
            {
                var xamlInfo = XamlSourceInfo.GetXamlSourceInfo(inspected);
                if (xamlInfo != null && xamlInfo.LineNumber > 0)
                {
                    line = xamlInfo.LineNumber;
                    col = xamlInfo.LinePosition;
                    sourceUri = xamlInfo.SourceUri?.ToString();
                    control = inspected;
                    break;
                }

                if (currentLineMapper != null && !string.IsNullOrEmpty(inspected.Name))
                {
                    var mapped = currentLineMapper.FindByName(inspected.Name);
                    if (mapped != null)
                    {
                        line = mapped.LineNumber;
                        col = mapped.LinePosition;
                        control = inspected;
                        break;
                    }
                }

                inspected = inspected.GetVisualParent() as Control;
            }

            if (line == 0 && currentLineMapper != null)
            {
                inspected = control;
                while (inspected != null && inspected != window)
                {
                    var mapped = currentLineMapper.FindByTag(inspected.GetType().Name);
                    if (mapped != null)
                    {
                        line = mapped.LineNumber;
                        col = mapped.LinePosition;
                        control = inspected;
                        break;
                    }
                    inspected = inspected.GetVisualParent() as Control;
                }
            }

            var transform = control.TransformToVisual(window);
            var rect = transform.HasValue
                ? new Rect(0, 0, control.Bounds.Width, control.Bounds.Height).TransformToAABB(transform.Value)
                : control.Bounds;

            var ancestors = control.GetVisualAncestors()
                .OfType<Control>()
                .Take(8)
                .Select(c => c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : $" #{c.Name}"))
                .ToList();

            var props = ExtractProperties(control);

            var response = new HitTestResponseMessage(
                RequestId: hit.RequestId,
                Found: true,
                TypeName: control.GetType().Name,
                ElementName: control.Name,
                LineNumber: line,
                LinePosition: col,
                SourceUri: sourceUri,
                BoundsX: rect.X,
                BoundsY: rect.Y,
                BoundsWidth: rect.Width,
                BoundsHeight: rect.Height,
                Classes: control.Classes.ToList(),
                AncestorTree: ancestors,
                Properties: props
            );

            await sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await ShowMeFraming.WriteControlMessageAsync(stream, response, ct).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }
        else
        {
            await sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await ShowMeFraming.WriteControlMessageAsync(stream, new HitTestResponseMessage(hit.RequestId, false), ct).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }
    }

    private static Dictionary<string, string> ExtractProperties(Control control)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddProp(props, "Name", control.Name);
        AddProp(props, "Width", double.IsNaN(control.Width) ? "Auto" : control.Width.ToString("0.#"));
        AddProp(props, "Height", double.IsNaN(control.Height) ? "Auto" : control.Height.ToString("0.#"));
        AddProp(props, "ActualSize", $"{control.Bounds.Width:0.#} × {control.Bounds.Height:0.#}");
        AddProp(props, "Margin", control.Margin.ToString());
        AddProp(props, "HorizontalAlignment", control.HorizontalAlignment.ToString());
        AddProp(props, "VerticalAlignment", control.VerticalAlignment.ToString());
        AddProp(props, "IsVisible", control.IsVisible.ToString());
        AddProp(props, "IsEnabled", control.IsEnabled.ToString());

        if (control is ContentControl cc && cc.Content != null)
        {
            AddProp(props, "Content", cc.Content is Control ? cc.Content.GetType().Name : cc.Content.ToString());
        }
        else if (control is TextBlock tb)
        {
            AddProp(props, "Text", tb.Text);
            AddProp(props, "FontSize", tb.FontSize.ToString("0.#"));
        }
        else if (control is TextBox txt)
        {
            AddProp(props, "Text", txt.Text);
        }

        if (control.DataContext != null)
        {
            AddProp(props, "DataContext", control.DataContext.GetType().Name);
        }

        return props;
    }

    private static void AddProp(Dictionary<string, string> dict, string key, string? val)
    {
        if (!string.IsNullOrWhiteSpace(val))
        {
            dict[key] = val;
        }
    }

    private void SendStatus(bool success, string? error, int? line, int? col)
    {
        Task.Run(async () =>
        {
            await sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await ShowMeFraming.WriteControlMessageAsync(stream, new XamlStatusMessage(success, error, line, col)).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        });
    }

    private static void TryLoadTargetAppResources(Assembly assembly)
    {
        try
        {
            var appType = assembly.GetTypes().FirstOrDefault(t => typeof(Application).IsAssignableFrom(t) && !t.IsAbstract);
            if (appType != null && Application.Current != null)
            {
                if (Activator.CreateInstance(appType) is Application targetApp)
                {
                    foreach (var style in targetApp.Styles)
                    {
                        Application.Current.Styles.Add(style);
                    }
                    foreach (var res in targetApp.Resources)
                    {
                        Application.Current.Resources[res.Key] = res.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host] No se pudieron importar estilos completos del target: {ex.Message}");
        }
    }

    private static RawInputModifiers ConvertModifiers(bool alt, bool control, bool shift)
    {
        var mod = RawInputModifiers.None;
        if (alt) mod |= RawInputModifiers.Alt;
        if (control) mod |= RawInputModifiers.Control;
        if (shift) mod |= RawInputModifiers.Shift;
        return mod;
    }

    private static MouseButton ConvertButton(PointerMouseButton button)
    {
        return button switch
        {
            PointerMouseButton.Left => MouseButton.Left,
            PointerMouseButton.Right => MouseButton.Right,
            PointerMouseButton.Middle => MouseButton.Middle,
            _ => MouseButton.None
        };
    }
}
