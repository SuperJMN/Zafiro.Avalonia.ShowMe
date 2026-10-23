using System.ComponentModel;
using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Xunit;
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
