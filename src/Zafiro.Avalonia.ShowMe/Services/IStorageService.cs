using Avalonia.Media.Imaging;

namespace Zafiro.Avalonia.ShowMe.Services;

public interface IStorageService
{
    Task<string?> OpenAxamlFileDialogAsync();
    Task<string?> SaveImageFileDialogAsync(string defaultFileName);
    Task<bool> CopyBitmapToClipboardAsync(WriteableBitmap bitmap);
}
