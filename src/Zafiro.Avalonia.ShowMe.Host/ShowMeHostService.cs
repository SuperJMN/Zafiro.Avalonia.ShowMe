using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;
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

                    var targetDir = !string.IsNullOrEmpty(init.TargetAssemblyPath)
                        ? Path.GetDirectoryName(init.TargetAssemblyPath)
                        : Directory.GetCurrentDirectory();

                    if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                    {
                        Program.TargetDirectory = targetDir;
                        PreloadTargetAssemblies(targetDir);
                        TryLoadTargetAppResources(targetDir);
                    }

                    var xamlAssemblyPath = !string.IsNullOrEmpty(init.XamlAssemblyPath) && File.Exists(init.XamlAssemblyPath)
                        ? init.XamlAssemblyPath
                        : init.TargetAssemblyPath;

                    if (!string.IsNullOrEmpty(xamlAssemblyPath) && File.Exists(xamlAssemblyPath))
                    {
                        try
                        {
                            targetAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(xamlAssemblyPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Host] Error cargando ensamblado XAML: {ex.Message}");
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
                            Dispatcher.UIThread.RunJobs();
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

            case ContextMenuHitTestRequestMessage contextReq:
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await HandleContextMenuHitTestAsync(contextReq, ct);
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

            if (!string.IsNullOrEmpty(theme))
            {
                if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
                {
                    window.RequestedThemeVariant = ThemeVariant.Dark;
                }
                else if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
                {
                    window.RequestedThemeVariant = ThemeVariant.Light;
                }
                else if (string.Equals(theme, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    window.RequestedThemeVariant = ThemeVariant.Default;
                }
            }

            window.Styles.Clear();
            window.Resources.Clear();
            window.Content = null;

            if (loaded is Window userWindow)
            {
                var content = userWindow.Content;
                userWindow.Content = null;
                window.Content = content;
                if (userWindow.Background != null)
                {
                    window.Background = userWindow.Background;
                }
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
            else if (loaded is Styles || loaded is IResourceDictionary || loaded is IStyle)
            {
                var title = loaded is Styles ? "Estilos" : loaded is IResourceDictionary ? "Diccionario de Recursos" : "Estilo";
                window.Content = ResourceCatalogBuilder.BuildCatalog(loaded, targetAssembly, title);
            }
            else if (loaded != null)
            {
                var group = ResourceExtractor.Extract(loaded, targetAssembly, "Recursos");
                if (group.TotalItemCount > 0)
                {
                    window.Content = ResourceCatalogBuilder.BuildCatalog(loaded, targetAssembly, "Recursos");
                }
                else
                {
                    window.Content = new ContentControl { Content = loaded };
                }
            }

            if (window.Content is Control rootControl)
            {
                if (rootControl.DataContext == null)
                {
                    var designDc = Design.GetDataContext(rootControl);
                    if (designDc != null)
                    {
                        rootControl.DataContext = designDc;
                    }
                }
            }

            if (loaded is not Window && Application.Current != null)
            {
                var currentVariant = window.ActualThemeVariant ?? Application.Current.ActualThemeVariant;
                if (Application.Current.Resources.TryGetResource("GradientBackgroundRadialGradient", currentVariant, out var radialBg) && radialBg is IBrush radialBrush)
                {
                    window.Background = radialBrush;
                }
                else if (window.Background == null && Application.Current.Resources.TryGetResource("ThemeBackgroundBrush", currentVariant, out var themeBg) && themeBg is IBrush themeBrush)
                {
                    window.Background = themeBrush;
                }
            }

            if (Application.Current != null)
            {
                foreach (var dt in Application.Current.DataTemplates)
                {
                    if (!window.DataTemplates.Contains(dt))
                    {
                        window.DataTemplates.Add(dt);
                    }
                }
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
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            var frame = window.CaptureRenderedFrame();
            if (frame == null) return;

            using var fb = frame.Lock();
            var format = fb.Format == global::Avalonia.Platform.PixelFormat.Bgra8888
                ? ShowMePixelFormat.Bgra8888
                : ShowMePixelFormat.Rgba8888;
            var stride = fb.RowBytes;
            var byteCount = stride * frame.PixelSize.Height;
            var buffer = new byte[byteCount];
            Marshal.Copy(fb.Address, buffer, 0, byteCount);

            Task.Run(async () =>
            {
                await sendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await ShowMeFraming.WriteFrameAsync(stream, frame.PixelSize.Width, frame.PixelSize.Height, stride, buffer, format).ConfigureAwait(false);
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

            var ancestors = hit.IsHover
                ? null
                : control.GetVisualAncestors()
                    .OfType<Control>()
                    .Take(8)
                    .Select(c => c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : $" #{c.Name}"))
                    .ToList();

            var props = hit.IsHover ? null : ExtractProperties(control);

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
                Properties: props,
                IsHover: hit.IsHover
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
                await ShowMeFraming.WriteControlMessageAsync(stream, new HitTestResponseMessage(hit.RequestId, false, IsHover: hit.IsHover), ct).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }
    }

    private async Task HandleContextMenuHitTestAsync(ContextMenuHitTestRequestMessage req, CancellationToken ct)
    {
        if (window == null) return;

        var pt = new Point(req.X, req.Y);
        var candidates = new HashSet<Control>();

        if (window.InputHitTest(pt) is Control inputControl)
        {
            foreach (var c in inputControl.GetSelfAndVisualAncestors().OfType<Control>())
            {
                if (c != window)
                {
                    candidates.Add(c);
                }
            }
        }

        foreach (var visual in window.GetVisualsAt(pt))
        {
            if (visual is Control control && control != window)
            {
                candidates.Add(control);
                foreach (var ancestor in control.GetVisualAncestors().OfType<Control>())
                {
                    if (ancestor != window)
                    {
                        candidates.Add(ancestor);
                    }
                }
            }
        }

        // Order candidates from deepest in the visual tree to least deep (leaf -> root)
        var sorted = candidates
            .OrderByDescending(c => c.GetVisualAncestors().Count())
            .ToList();

        var items = new List<VisualItemInfo>();
        var seen = new HashSet<string>();

        foreach (var c in sorted)
        {
            int line = 0;
            int col = 0;
            string? sourceUri = null;

            var xamlInfo = XamlSourceInfo.GetXamlSourceInfo(c);
            if (xamlInfo != null && xamlInfo.LineNumber > 0)
            {
                line = xamlInfo.LineNumber;
                col = xamlInfo.LinePosition;
                sourceUri = xamlInfo.SourceUri?.ToString();
            }
            else if (currentLineMapper != null && !string.IsNullOrEmpty(c.Name))
            {
                var mapped = currentLineMapper.FindByName(c.Name);
                if (mapped != null)
                {
                    line = mapped.LineNumber;
                    col = mapped.LinePosition;
                }
            }
            else if (currentLineMapper != null)
            {
                var mapped = currentLineMapper.FindByTag(c.GetType().Name);
                if (mapped != null)
                {
                    line = mapped.LineNumber;
                    col = mapped.LinePosition;
                }
            }

            // Exclude external framework theme internals (e.g. avares://Avalonia.Themes.Fluent/...)
            if (sourceUri != null && sourceUri.StartsWith("avares://Avalonia.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Calculate transformed bounds relative to the window
            var transform = c.TransformToVisual(window);
            var rect = transform.HasValue
                ? new Rect(0, 0, c.Bounds.Width, c.Bounds.Height).TransformToAABB(transform.Value)
                : c.Bounds;

            // Deduplicate items that have identical type, name, file, and line/col
            var key = $"{c.GetType().Name}|{c.Name}|{sourceUri}|{line}:{col}";
            if (seen.Add(key))
            {
                items.Add(new VisualItemInfo(
                    TypeName: c.GetType().Name,
                    ElementName: c.Name,
                    LineNumber: line,
                    LinePosition: col,
                    SourceUri: sourceUri,
                    BoundsX: rect.X,
                    BoundsY: rect.Y,
                    BoundsWidth: rect.Width,
                    BoundsHeight: rect.Height
                ));
            }
        }

        // If some items have resolved line numbers > 0, filter to only those that can be navigated to
        if (items.Any(i => i.LineNumber > 0))
        {
            items = items.Where(i => i.LineNumber > 0).ToList();
        }

        var response = new ContextMenuHitTestResponseMessage(req.RequestId, req.X, req.Y, items);

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

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return Enumerable.Empty<Type>();
        }
    }

    private static void PreloadTargetAssemblies(string targetDir)
    {
        if (!Directory.Exists(targetDir)) return;

        var loadedNames = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name!)
                .Where(n => !string.IsNullOrEmpty(n)),
            StringComparer.OrdinalIgnoreCase);

        var dllFiles = Directory.GetFiles(targetDir, "*.dll");
        foreach (var dllPath in dllFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(dllPath);

            // Evitar colisión con ensamblados del host y core de Avalonia ya cargados
            if (loadedNames.Contains(fileName))
            {
                continue;
            }

            try
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                loadedNames.Add(fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Host] Aviso al precargar {fileName}: {ex.Message}");
            }
        }

        try
        {
            var configFiles = Directory.GetFiles(targetDir, "*.runtimeconfig.json");
            foreach (var cfgPath in configFiles)
            {
                var json = File.ReadAllText(cfgPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("runtimeOptions", out var ro) &&
                    ro.TryGetProperty("configProperties", out var cp) &&
                    cp.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in cp.EnumerateObject())
                    {
                        var strVal = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                            ? prop.Value.GetString()
                            : prop.Value.GetRawText();
                        AppContext.SetData(prop.Name, strVal);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Host] Aviso al cargar runtimeconfig.json: {ex.Message}");
        }
    }

    private static void TryLoadTargetAppResources(string targetDir)
    {
        try
        {
            Type? appType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                if (!asm.Location.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase)) continue;

                var candidate = GetLoadableTypes(asm).FirstOrDefault(t =>
                    typeof(Application).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t != typeof(App));

                if (candidate != null)
                {
                    appType = candidate;
                    break;
                }
            }

            if (appType != null && Application.Current != null)
            {
                Console.WriteLine($"[Host] Encontrada clase Application: {appType.FullName}");
                if (Activator.CreateInstance(appType) is Application targetApp)
                {
                    try
                    {
                        targetApp.Initialize();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Host] Aviso al inicializar Application destino ({appType.FullName}): {ex}");
                    }

                    Console.WriteLine($"[Host] targetApp inicializado. Styles={targetApp.Styles.Count}, Resources={targetApp.Resources.Count}, MergedDicts={(targetApp.Resources is ResourceDictionary r ? r.MergedDictionaries.Count : 0)}");

                    var hasFluentTheme = Application.Current.Styles.OfType<FluentTheme>().Any();
                    var stylesToMove = targetApp.Styles.ToList();
                    targetApp.Styles.Clear();
                    foreach (var style in stylesToMove)
                    {
                        if (style is FluentTheme && hasFluentTheme)
                        {
                            continue;
                        }
                        Application.Current.Styles.Add(style);
                    }

                    if (Application.Current.Resources is ResourceDictionary hostDict && targetApp.Resources is ResourceDictionary targetDict)
                    {
                        var merged = targetDict.MergedDictionaries.ToList();
                        targetDict.MergedDictionaries.Clear();
                        foreach (var md in merged)
                        {
                            hostDict.MergedDictionaries.Add(md);
                        }
                    }

                    foreach (var res in targetApp.Resources)
                    {
                        Application.Current.Resources[res.Key] = res.Value;
                    }

                    var templatesToMove = targetApp.DataTemplates.ToList();
                    targetApp.DataTemplates.Clear();
                    foreach (var dt in templatesToMove)
                    {
                        Application.Current.DataTemplates.Add(dt);
                    }

                    Console.WriteLine($"[Host] Application.Current actualizado. Styles={Application.Current.Styles.Count}, Resources={Application.Current.Resources.Count}, MergedDicts={(Application.Current.Resources is ResourceDictionary r2 ? r2.MergedDictionaries.Count : 0)}");

                    if (targetApp.ActualThemeVariant != null && targetApp.RequestedThemeVariant != null)
                    {
                        Application.Current.RequestedThemeVariant = targetApp.RequestedThemeVariant;
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
