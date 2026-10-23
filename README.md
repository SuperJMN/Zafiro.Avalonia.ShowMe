<div align="center">
  <img src="assets/icon.png" width="128" height="128" alt="Zafiro.Avalonia.ShowMe Icon" />
  <h1>Zafiro.Avalonia.ShowMe</h1>
  <p><strong>Previsualizador interactivo para vistas y controles de Avalonia UI</strong></p>
</div>

Aplicación y herramienta CLI para previsualizar XAML/AXAML arbitrario de otras aplicaciones Avalonia en su propio contexto de ejecución, utilizando el **Avalonia Previewer oficial** (`Avalonia.Designer.HostApp`) alojado dentro de un control **PanAndZoom**.

---

## 🌟 Características

- **Previsualización en el contexto real de la aplicación:**
  - Detecta automáticamente el proyecto `.csproj` contenedor del archivo AXAML.
  - Si es una biblioteca de clases, localiza la aplicación ejecutable anfitriona (p. ej. `*.Desktop`).
  - Obtiene la configuración MSBuild (`AvaloniaPreviewerNetCoreToolPath`, `TargetPath`, `runtimeconfig.json`, `deps.json`).
  - Compila automáticamente el proyecto host si el binario no existe aún.
  - Conecta mediante protocolo binario BSON TCP oficial de Avalonia (`Avalonia.Remote.Protocol`).
- **Control Pan & Zoom integrado:**
  - Alojado en el control `ZoomBorder` de Wieslaw Soltes (`PanAndZoom`).
  - Desplazamiento panorámico (pan) mediante arrastre con el ratón.
  - Zoom interactivo mediante la rueda del ratón y botones dedicados (`+`, `−`, `1:1`, `Ajustar`).
- **Ver el XAML de un elemento:**
  - Con el elemento seleccionado, «Ver XAML» (en la barra de estado, en el submenú de cada elemento del menú contextual del inspector o con doble clic) abre un visor de solo lectura dentro de ShowMe, con resaltado XML, en el archivo donde está definido el elemento y con su línea resaltada.
  - Si el elemento viene de otro archivo (por ejemplo, un `UserControl`), el visor muestra ese archivo. El visor se cierra con su botón ✕ o con Esc.
  - «Abrir en el editor» solo aparece cuando hay un editor de código configurado.
- **Selector de Tema:**
  - Selector con opciones: **Aplicación**, **Claro** y **Oscuro**.
  - Cambia tanto el tema de la ventana de ShowMe como el tema del XAML renderizado en el previewer (`RequestedThemeVariant` / `ThemeVariantScope`).
- **Guardado y Exportación:**
  - Guardar el fotograma renderizado en formato PNG o JPEG con selector de archivo nativo.
  - Copiar la imagen renderizada directamente al portapapeles.
- **Hot-Reload en tiempo real:**
  - Observa el archivo AXAML en disco con `FileSystemWatcher`.
  - Al guardar cambios en tu editor (VS Code, Rider, etc.), la vista previa se actualiza instantáneamente sin reiniciar el proceso.
- **Línea de Comandos (CLI) & GUI:**
  - Instalable como herramienta global de .NET (`dotnet tool install -g Zafiro.Avalonia.ShowMe`).
  - Ejecutable directamente como `zafiro-avalonia-showme [archivo.axaml]`.
  - Zona de arrastrar y soltar (Drag & Drop) cuando se inicia sin argumentos.

---

## 📦 Instalación como .NET Tool

```bash
# Instalación global desde NuGet
dotnet tool install -g Zafiro.Avalonia.ShowMe

# O actualización a la última versión
dotnet tool update -g Zafiro.Avalonia.ShowMe
```

### 💻 Plataformas soportadas

ShowMe necesita el SDK de .NET 10 y un escritorio gráfico donde Avalonia pueda renderizar. El paquete incluye los binarios nativos de:

| Sistema | Arquitecturas |
|---|---|
| Windows | x64, Arm64 |
| macOS | x64, Apple Silicon |
| Linux (glibc) | x64, Arm64 |

La lista se define en `SupportedNativeRuntimeIdentifiers`, en `src/Zafiro.Avalonia.ShowMe/Zafiro.Avalonia.ShowMe.csproj`.

### 🖥️ Integración con el escritorio (Linux / GNOME / COSMIC / KDE)

Para asociar archivos `.axaml` y `.xaml` y poder abrirlos directamente con doble clic o menú contextual del gestor de archivos con el icono de la aplicación:

```bash
./scripts/register-desktop-integration.sh
```

---

## 🚀 Uso desde la Línea de Comandos (CLI)

```bash
# Previsualizar un archivo AXAML directamente
zafiro-avalonia-showme ruta/al/archivo.axaml

# Especificar tema inicial ('Default', 'Light', 'Dark')
zafiro-avalonia-showme ruta/al/archivo.axaml --theme Dark

# Especificar resolución inicial del viewport
zafiro-avalonia-showme ruta/al/archivo.axaml -w 1280 -h 800

# Editor externo para «Abrir en el editor» ({file}, {line} y {column} se sustituyen)
zafiro-avalonia-showme ruta/al/archivo.axaml --editor "rider --line {line} --column {column} {file}"

# Ver ayuda de opciones
zafiro-avalonia-showme --help
```

Ver el XAML siempre ocurre dentro de ShowMe; el editor externo es opcional. El editor se elige, por este orden:

1. `--editor` en la línea de comandos.
2. El editor guardado desde la app, en **Más acciones → Editor de código…**. Se guarda en `settings.json`, dentro de la carpeta de configuración del usuario (`~/.config/Zafiro.Avalonia.ShowMe/` en Linux), así que también se aplica cuando ShowMe se abre desde el gestor de archivos.
3. `$VISUAL` o `$EDITOR`. ShowMe conoce cómo saltar a la línea en VS Code, Cursor, Rider, Zed, Sublime Text, Vim/Neovim, Helix, nano, micro, Kakoune y Emacs. Los editores de terminal se abren dentro de `$TERMINAL` o, si no está definida, de `x-terminal-emulator`.

Si no hay ningún editor, «Abrir en el editor» no aparece. ShowMe nunca entrega un `.axaml` al manejador del sistema, porque puede estar asociado al propio ShowMe. Cualquier fallo al abrir el editor aparece en la barra de estado.

---

## 🏗️ Arquitectura

```
Zafiro.Avalonia.ShowMe/
├── src/Zafiro.Avalonia.ShowMe/
│   ├── Core/
│   │   ├── CodeEditor.cs              # Editor externo opcional (--editor, ajuste guardado, $VISUAL/$EDITOR)
│   │   ├── CommandLineArgs.cs         # Parser de argumentos CLI
│   │   ├── EditorCommand.cs           # Comando del editor para ir al código
│   │   ├── UserSettings.cs            # Ajustes del usuario en settings.json
│   │   ├── ExecutableLocator.cs       # Búsqueda de ejecutables en PATH (y PATHEXT en Windows)
│   │   ├── PreviewTarget.cs           # Metadatos del archivo, proyecto y binarios host
│   │   ├── PreviewTargetResolver.cs   # Descubrimiento de proyecto host y propiedades MSBuild
│   │   ├── PreviewServer.cs           # Host TCP BSON y proceso oficial Avalonia Previewer
│   │   ├── XamlThemeModifier.cs       # Inyección en memoria de ThemeVariant
│   │   └── DelegateCommand.cs         # Comandos seguros para el hilo de UI
│   ├── Services/
│   │   ├── IStorageService.cs         # Diálogos de guardado/apertura y portapapeles
│   │   └── AvaloniaStorageService.cs  # Implementación con StorageProvider y Clipboard de Avalonia
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           # ViewModel principal, carga y temas
│   │   ├── PreviewSessionViewModel.cs # ViewModel de la sesión activa, zoom y frames
│   │   └── ThemeOption.cs             # Opciones de tema (Default, Light, Dark)
│   └── Views/
│       ├── MainWindow.axaml           # Toolbar, ZoomBorder (PanAndZoom) y StatusBar
│       ├── MainWindow.axaml.cs        # Drag & Drop y eventos de zoom
│       └── XamlSourceView.cs          # Visor de XAML de solo lectura (AvaloniaEdit)
└── test/Zafiro.Avalonia.ShowMe.Tests/ # Pruebas unitarias e integración
```

---

## 📜 Atribuciones

- Icono de la aplicación: [Surprise icon](https://www.flaticon.com/free-icon/surprise_3656962) creado por [Magnific](https://www.flaticon.com/authors/magnific) desde [Flaticon](https://www.flaticon.com/).
