# Zafiro.Avalonia.ShowMe

ShowMe is an Avalonia desktop app, shipped as a .NET tool, that previews
arbitrary AXAML inside the context of the application it belongs to. The app is
`src/Zafiro.Avalonia.ShowMe`; the tests are `test/Zafiro.Avalonia.ShowMe.Tests`.

## Commands

- Create every command with `EnhancedCommand` (Zafiro.UI) and expose it as
  `IEnhancedCommand`. Never use `DelegateCommand` or a raw
  `ReactiveCommand.Create`.
- `Program.BuildAvaloniaApp` calls `UseReactiveUI`. Keep it: without it,
  `CanExecuteChanged` is raised off the UI thread and the app crashes (#1).

## Tests

- Never test views or their AXAML. Test view models, services and domain code,
  following Khorikov's principles: observable outcomes, real in-memory
  collaborators, fakes only at true boundaries.
- The whole suite must pass in one go with the release gate command, run from
  the repository root (#10):

  ```bash
  dotnet test Zafiro.Avalonia.ShowMe.slnx -c Release --nologo -m:1
  ```

## Release

- Every merge to `master` publishes the tool to NuGet through
  DotnetDeployer.Fleet (`deployer.yaml`), after the test gate above passes.
- The version comes from GitVersion (`GitVersion.yml`), counting from the tag
  `v0.1.0`.
- The tool package only ships the native assets of the platforms listed in
  `SupportedNativeRuntimeIdentifiers`
  (`src/Zafiro.Avalonia.ShowMe/Zafiro.Avalonia.ShowMe.csproj`). The package
  must stay under the 64 MB the Fleet coordinator accepts (#15,
  SuperJMN/DotnetDeployer.Fleet#82).

## Checking the UI with the Zafiro.Avalonia MCP

- Launch with `launch_app`, passing the `.csproj` of `src/Zafiro.Avalonia.ShowMe`
  as the project and an AXAML file as the app argument. Wait for the text
  "XAML cargado" before interacting, and always finish with `close_app`.
- The MCP cannot right-click. Open the inspector context menu with
  `find_by_datacontext`, selector `Window`, and this predicate:

  ```csharp
  CurrentSession != null && new System.Func<bool>(() => { CurrentSession.RequestInspectContextMenu(new Avalonia.Point(300, 120)); return true; })()
  ```

- The MCP cannot drive native dialogs such as the file picker. To check that
  one opens, watch the portal call (`OpenFile` or `SaveFile`) instead of
  interacting with the dialog:

  ```bash
  dbus-monitor --session "interface='org.freedesktop.portal.FileChooser'"
  ```
