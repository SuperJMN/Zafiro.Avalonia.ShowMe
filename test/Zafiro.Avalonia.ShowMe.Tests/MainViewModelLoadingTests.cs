using System.ComponentModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Xunit;
using Zafiro.Avalonia.ShowMe.Protocol;
using Zafiro.Avalonia.ShowMe.Services;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Tests;

public class MainViewModelLoadingTests
{
    private const string ValidProgram = "System.Console.WriteLine(\"Hola\");";
    private const string BrokenProgram = "System.Console.WriteLine(\"Hola\")";

    private class FakeStorageService : IStorageService
    {
        public Task<string?> OpenAxamlFileDialogAsync() => Task.FromResult<string?>(null);
        public Task<string?> SaveImageFileDialogAsync(string defaultFileName) => Task.FromResult<string?>(null);
        public Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap) => Task.FromResult(true);
    }

    [AvaloniaFact]
    public async Task Loading_A_Project_That_Needs_Building_Shows_Every_Step_Until_The_Preview_Is_Ready()
    {
        using var project = ScratchProject.Create("PreviewApp", ValidProgram);
        var vm = new MainViewModel(new FakeStorageService());
        var messages = new List<string>();
        var details = new List<string?>();
        using var messagesSubscription = Changes(vm, nameof(MainViewModel.LoadingMessage), x => x.LoadingMessage).Subscribe(messages.Add);
        using var detailsSubscription = Changes(vm, nameof(MainViewModel.LoadingDetail), x => x.LoadingDetail).Subscribe(details.Add);

        await vm.LoadFileAsync(project.AxamlPath);
        using var session = vm.CurrentSession;

        Assert.False(vm.HasError, vm.ErrorMessage);
        Assert.NotNull(session);
        Assert.False(vm.IsLoading);
        Assert.Equal(
        [
            "Abriendo 'View.axaml'...",
            "Buscando el proyecto...",
            "Evaluando MSBuild...",
            "Compilando PreviewApp...",
            "Iniciando el previewer...",
            "Renderizando..."
        ], messages);
        Assert.Contains(details, d => d != null && d.StartsWith("PreviewApp ->", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task A_Failing_Build_Names_The_Build_Step_And_Shows_Its_Output_While_Building()
    {
        using var project = ScratchProject.Create("BrokenApp", BrokenProgram);
        var vm = new MainViewModel(new FakeStorageService());
        var details = new List<string?>();
        using var detailsSubscription = Changes(vm, nameof(MainViewModel.LoadingDetail), x => x.LoadingDetail).Subscribe(details.Add);

        await vm.LoadFileAsync(project.AxamlPath);

        Assert.True(vm.HasError);
        Assert.StartsWith("Falló el paso «Compilando BrokenApp»", vm.ErrorMessage);
        Assert.Contains(details, d => d != null && d.Contains("error CS", StringComparison.Ordinal));
        Assert.Null(vm.CurrentSession);
        Assert.False(vm.IsLoading);
    }

    [AvaloniaFact]
    public async Task A_File_Outside_Any_Project_Fails_At_Finding_The_Project()
    {
        using var folder = ScratchProject.WithoutProject();
        var vm = new MainViewModel(new FakeStorageService());

        await vm.LoadFileAsync(folder.AxamlPath);

        Assert.True(vm.HasError);
        Assert.StartsWith("Falló el paso «Buscando el proyecto»", vm.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task Welcome_Card_Is_Hidden_While_Loading()
    {
        using var folder = ScratchProject.WithoutProject();
        var vm = new MainViewModel(new FakeStorageService());
        var welcomeVisibility = new List<bool>();
        using var subscription = Changes(vm, nameof(MainViewModel.IsWelcomeVisible), x => x.IsWelcomeVisible).Subscribe(welcomeVisibility.Add);

        Assert.True(vm.IsWelcomeVisible);
        await vm.LoadFileAsync(folder.AxamlPath);

        Assert.Equal([false, true], welcomeVisibility);
    }

    [AvaloniaFact]
    public async Task Going_Back_From_An_Opened_View_Restores_The_Previous_View_With_Its_Size_And_Selection()
    {
        using var project = ScratchProject.Create("NavigationApp", ValidProgram);
        project.WriteView("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                         xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                         mc:Ignorable="d" d:DesignWidth="640" d:DesignHeight="480">
              <TextBlock Text="Hola" />
            </UserControl>
            """);
        var otherView = project.AddView("Other.axaml");
        var vm = new MainViewModel(new FakeStorageService());
        await vm.LoadFileAsync(project.AxamlPath);
        vm.CurrentSession!.SetCustomDimensions(500, 400);
        vm.CurrentSession.OnPointerInput(PointerActionType.Down, new Point(5, 5), PointerMouseButton.Left, default, false, ctrl: true, false);
        var selected = await Eventually(() => vm.CurrentSession.SelectedElement);

        await vm.OpenViewAsync(otherView);
        var openedView = vm.CurrentSession?.Target.AxamlPath;
        var couldGoBack = vm.CanGoBack;
        await vm.GoBackAsync();
        using var session = vm.CurrentSession;
        var restored = await Eventually(() => session?.SelectedElement);
        var frame = await Eventually(() => session?.CurrentBitmap);

        Assert.False(vm.HasError, vm.ErrorMessage);
        Assert.Equal(otherView, openedView);
        Assert.True(couldGoBack);
        Assert.Equal(project.AxamlPath, session?.Target.AxamlPath);
        Assert.Equal(new PixelSize(500, 400), session?.ChosenSize);
        Assert.Equal(new PixelSize(500, 400), frame.PixelSize);
        Assert.Equal(new DimensionPreset("Diseño original", 640, 480), session?.Presets[0]);
        Assert.Equal((selected.TypeName, selected.LineNumber), (restored.TypeName, restored.LineNumber));
        Assert.False(vm.CanGoBack);
    }

    [AvaloniaFact]
    public async Task Going_Back_Twice_Without_Waiting_Shows_The_First_View_With_A_Single_Previewer_Alive()
    {
        using var project = ScratchProject.Create("DoubleBackApp", ValidProgram);
        var secondView = project.AddView("Second.axaml");
        var thirdView = project.AddView("Third.axaml");
        var vm = new MainViewModel(new FakeStorageService());
        await vm.LoadFileAsync(project.AxamlPath);
        await vm.OpenViewAsync(secondView);
        await vm.OpenViewAsync(thirdView);
        var shownSessions = new List<PreviewSessionViewModel>();
        using var shownSubscription = Changes(vm, nameof(MainViewModel.CurrentSession), x => x.CurrentSession)
            .Where(shown => shown != null)
            .Subscribe(shown => shownSessions.Add(shown!));

        var firstBack = vm.GoBackAsync();
        var secondBack = vm.GoBackAsync();
        await Task.WhenAll(firstBack, secondBack);
        using var session = vm.CurrentSession;
        var livePreviewers = await Settled(() => LivePreviewersIn(project.Root), expected: 1);

        Assert.False(vm.HasError, vm.ErrorMessage);
        Assert.False(vm.IsLoading);
        Assert.Equal([project.AxamlPath], shownSessions.Select(s => s.Target.AxamlPath));
        Assert.Same(shownSessions[0], session);
        Assert.Equal(1, livePreviewers);
        Assert.False(vm.CanGoBack);
    }

    /// <summary>
    /// The ShowMe.Host previewers (started with "--port") running from the output of the project in <paramref name="root"/>.
    /// </summary>
    private static int LivePreviewersIn(string root) =>
        Directory.GetDirectories("/proc").Count(process => IsPreviewerIn(process, root));

    private static bool IsPreviewerIn(string process, string root)
    {
        try
        {
            var cwd = new DirectoryInfo(Path.Combine(process, "cwd")).LinkTarget;
            var arguments = File.ReadAllText(Path.Combine(process, "cmdline")).Split('\0');
            return cwd != null && cwd.StartsWith(root, StringComparison.Ordinal) && arguments.Contains("--port");
        }
        catch (Exception)
        {
            // Not a process, gone meanwhile, or not ours to read.
            return false;
        }
    }

    /// <summary>
    /// Reads a value until it reaches <paramref name="expected"/> or a few seconds pass, and returns the last one read.
    /// </summary>
    private static async Task<int> Settled(Func<int> read, int expected)
    {
        var value = read();
        for (var attempt = 0; attempt < 50 && value != expected; attempt++)
        {
            await Task.Delay(100);
            value = read();
        }

        return value;
    }

    [AvaloniaFact]
    public async Task Going_Back_Restores_The_Selection_Even_When_The_Size_Changed_After_It_Was_Picked()
    {
        using var project = ScratchProject.Create("MovedSelectionApp", ValidProgram);
        project.WriteView("""
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Grid>
                <Border x:Name="Corner" Background="Red" Width="60" Height="40"
                        HorizontalAlignment="Right" VerticalAlignment="Bottom" />
              </Grid>
            </UserControl>
            """);
        var otherView = project.AddView("Other.axaml");
        var vm = new MainViewModel(new FakeStorageService());
        await vm.LoadFileAsync(project.AxamlPath, explicitWidth: 800, explicitHeight: 600);
        vm.CurrentSession!.OnPointerInput(PointerActionType.Down, new Point(770, 580), PointerMouseButton.Left, default, false, ctrl: true, false);
        var selected = await Eventually(() => vm.CurrentSession.SelectedElement);
        vm.CurrentSession.SetCustomDimensions(500, 400);

        await vm.OpenViewAsync(otherView);
        await vm.GoBackAsync();
        using var session = vm.CurrentSession;
        var restored = await Eventually(() => session?.SelectedElement);

        Assert.False(vm.HasError, vm.ErrorMessage);
        Assert.Equal("Corner", selected.ElementName);
        Assert.Equal("Corner", restored.ElementName);
        Assert.Equal(new Rect(440, 360, 60, 40), restored.Bounds);
    }

    [AvaloniaFact]
    public async Task Going_Back_Restores_A_Selection_Picked_After_Scrolling_And_Scrolls_It_Into_View()
    {
        using var project = ScratchProject.Create("ScrolledSelectionApp", ValidProgram);
        project.WriteView("""
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <ScrollViewer>
                <StackPanel>
                  <Border Height="2000" />
                  <Border x:Name="Footer" Background="Red" Height="40" />
                </StackPanel>
              </ScrollViewer>
            </UserControl>
            """);
        var otherView = project.AddView("Other.axaml");
        var vm = new MainViewModel(new FakeStorageService());
        await vm.LoadFileAsync(project.AxamlPath, explicitWidth: 400, explicitHeight: 300);
        vm.CurrentSession!.OnPointerInput(PointerActionType.Wheel, new Point(100, 100), PointerMouseButton.None, new Vector(0, -1000), false, false, false);
        vm.CurrentSession.OnPointerInput(PointerActionType.Down, new Point(100, 280), PointerMouseButton.Left, default, false, ctrl: true, false);
        var selected = await Eventually(() => vm.CurrentSession.SelectedElement);

        await vm.OpenViewAsync(otherView);
        await vm.GoBackAsync();
        using var session = vm.CurrentSession;
        var restored = await Eventually(() => session?.SelectedElement);

        Assert.False(vm.HasError, vm.ErrorMessage);
        Assert.Equal("Footer", selected.ElementName);
        Assert.Equal("Footer", restored.ElementName);
        Assert.InRange(restored.Bounds.Bottom, 40, 300);
    }

    private static async Task<T> Eventually<T>(Func<T?> read) where T : class
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (read() is { } value) return value;
            await Task.Delay(100);
        }

        throw new TimeoutException("The preview did not answer in time.");
    }

    private static IObservable<T> Changes<T>(MainViewModel vm, string propertyName, Func<MainViewModel, T> read)
    {
        return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                h => vm.PropertyChanged += h,
                h => vm.PropertyChanged -= h)
            .Where(e => e.EventArgs.PropertyName == propertyName)
            .Select(_ => read(vm))
            .DistinctUntilChanged();
    }

    private sealed class ScratchProject : IDisposable
    {
        private readonly string root;

        private ScratchProject(string root, string axamlPath)
        {
            this.root = root;
            AxamlPath = axamlPath;
        }

        public string AxamlPath { get; }

        public string Root => root;

        public void WriteView(string xaml) => File.WriteAllText(AxamlPath, xaml);

        public string AddView(string name)
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, """
                <UserControl xmlns="https://github.com/avaloniaui">
                  <TextBlock Text="Otra" />
                </UserControl>
                """);
            return path;
        }

        public static ScratchProject Create(string name, string program)
        {
            var scratch = WithoutProject();
            var projectDir = Path.GetDirectoryName(scratch.AxamlPath)!;
            File.WriteAllText(Path.Combine(projectDir, $"{name}.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), program);
            return scratch;
        }

        public static ScratchProject WithoutProject()
        {
            var root = Path.Combine(Path.GetTempPath(), $"showme-loading-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var axamlPath = Path.Combine(root, "View.axaml");
            File.WriteAllText(axamlPath, """
                <UserControl xmlns="https://github.com/avaloniaui">
                  <TextBlock Text="Hola" />
                </UserControl>
                """);
            return new ScratchProject(root, axamlPath);
        }

        public void Dispose()
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
