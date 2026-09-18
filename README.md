# Zafiro.Avalonia.ShowMe

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

---

## 🚀 Uso desde la Línea de Comandos (CLI)

```bash
# Previsualizar un archivo AXAML directamente
zafiro-avalonia-showme ruta/al/archivo.axaml

# Especificar tema inicial ('Default', 'Light', 'Dark')
zafiro-avalonia-showme ruta/al/archivo.axaml --theme Dark

# Especificar resolución inicial del viewport
zafiro-avalonia-showme ruta/al/archivo.axaml -w 1280 -h 800

# Ver ayuda de opciones
zafiro-avalonia-showme --help
```

---

## 🏗️ Arquitectura

```
Zafiro.Avalonia.ShowMe/
├── src/Zafiro.Avalonia.ShowMe/
│   ├── Core/
│   │   ├── CommandLineArgs.cs         # Parser de argumentos CLI
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
│       └── MainWindow.axaml.cs        # Drag & Drop y eventos de zoom
└── test/Zafiro.Avalonia.ShowMe.Tests/ # Pruebas unitarias e integración
```
