using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Zafiro.Avalonia.ShowMe.Services;

public sealed class AvaloniaStorageService : IStorageService
{
    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    public async Task<string?> OpenAxamlFileDialogAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seleccionar archivo XAML / AXAML",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Archivos XAML / AXAML")
                {
                    Patterns = ["*.axaml", "*.xaml"]
                },
                new FilePickerFileType("Todos los archivos")
                {
                    Patterns = ["*.*"]
                }
            ]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> SaveImageFileDialogAsync(string defaultFileName)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar imagen renderizada",
            DefaultExtension = "png",
            SuggestedFileName = defaultFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Imagen PNG (*.png)") { Patterns = ["*.png"] },
                new FilePickerFileType("Imagen JPEG (*.jpg)") { Patterns = ["*.jpg", "*.jpeg"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    public async Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return false;

        var clipboard = topLevel.Clipboard;
        if (clipboard == null) return false;

        try
        {
            var dataTransfer = new DataTransfer();
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            dataTransfer.Add(item);
            await clipboard.SetDataAsync(dataTransfer);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
