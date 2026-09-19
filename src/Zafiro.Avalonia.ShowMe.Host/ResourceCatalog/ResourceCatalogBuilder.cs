using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Zafiro.Avalonia.ShowMe.Host.ResourceCatalog;

public static class ResourceCatalogBuilder
{
    public static Control BuildCatalog(object loaded, Assembly? targetAssembly, string rootName)
    {
        Control? previewWith = null;

        if (loaded is AvaloniaObject avObj)
        {
            try
            {
                previewWith = Design.GetPreviewWith(avObj);
            }
            catch
            {
                // Ignorar error al resolver PreviewWith
            }
        }

        if (previewWith == null && loaded is IStyle styleObj)
        {
            try
            {
                previewWith = Design.GetPreviewWith(styleObj);
            }
            catch
            {
                // Ignorar error al resolver PreviewWith
            }
        }

        var rootGroup = ResourceExtractor.Extract(loaded, targetAssembly, rootName);
        var catalogView = new ResourceCatalogView(rootGroup, previewWith);

        if (loaded is Styles styles)
        {
            catalogView.AttachStyles(styles);
        }
        else if (loaded is IStyle singleStyle)
        {
            var st = new Styles { singleStyle };
            catalogView.AttachStyles(st);
        }

        if (loaded is IResourceDictionary resDict)
        {
            catalogView.AttachResources(resDict);
        }
        else if (loaded is Styles stWithRes && stWithRes.Resources.Count > 0)
        {
            catalogView.AttachResources(stWithRes.Resources);
        }

        return catalogView;
    }
}
