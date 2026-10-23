using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.ShowMe.Protocol;

public enum ShowMePacketType : byte
{
    ControlMessage = 1,
    Frame = 2
}

public enum PointerActionType
{
    Move,
    Down,
    Up,
    Wheel
}

public enum PointerMouseButton
{
    None,
    Left,
    Right,
    Middle
}

public enum KeyActionType
{
    Down,
    Up,
    TextInput
}

[JsonDerivedType(typeof(InitMessage), "Init")]
[JsonDerivedType(typeof(UpdateXamlMessage), "UpdateXaml")]
[JsonDerivedType(typeof(ResizeViewportMessage), "ResizeViewport")]
[JsonDerivedType(typeof(PointerInputMessage), "PointerInput")]
[JsonDerivedType(typeof(KeyInputMessage), "KeyInput")]
[JsonDerivedType(typeof(HitTestRequestMessage), "HitTestRequest")]
[JsonDerivedType(typeof(HitTestResponseMessage), "HitTestResponse")]
[JsonDerivedType(typeof(ContextMenuHitTestRequestMessage), "ContextMenuHitTestRequest")]
[JsonDerivedType(typeof(ContextMenuHitTestResponseMessage), "ContextMenuHitTestResponse")]
[JsonDerivedType(typeof(FindElementRequestMessage), "FindElementRequest")]
[JsonDerivedType(typeof(FindElementResponseMessage), "FindElementResponse")]
[JsonDerivedType(typeof(XamlStatusMessage), "XamlStatus")]
[JsonDerivedType(typeof(CatalogInfoMessage), "CatalogInfo")]
[JsonDerivedType(typeof(SelectCatalogItemMessage), "SelectCatalogItem")]
public abstract record ShowMeMessage;

public record InitMessage(
    string TargetAssemblyPath,
    string InitialXaml,
    string Theme,
    double Width,
    double Height,
    double Dpi = 96.0,
    string? XamlAssemblyPath = null,
    string? XamlFilePath = null
) : ShowMeMessage;

public record UpdateXamlMessage(
    string Xaml,
    string? Theme = null,
    string? XamlFilePath = null
) : ShowMeMessage;

public record ResizeViewportMessage(
    double Width,
    double Height,
    double Dpi = 96.0
) : ShowMeMessage;

public record PointerInputMessage(
    PointerActionType Action,
    double X,
    double Y,
    PointerMouseButton Button = PointerMouseButton.None,
    double DeltaX = 0,
    double DeltaY = 0,
    bool Alt = false,
    bool Control = false,
    bool Shift = false
) : ShowMeMessage;

public record KeyInputMessage(
    KeyActionType Action,
    int KeyCode = 0,
    string? Text = null,
    bool Alt = false,
    bool Control = false,
    bool Shift = false
) : ShowMeMessage;

public record HitTestRequestMessage(
    string RequestId,
    double X,
    double Y,
    bool IsHover = false
) : ShowMeMessage;

public record HitTestResponseMessage(
    string RequestId,
    bool Found,
    string TypeName = "",
    string? ElementName = null,
    int LineNumber = 0,
    int LinePosition = 0,
    string? SourceUri = null,
    double BoundsX = 0,
    double BoundsY = 0,
    double BoundsWidth = 0,
    double BoundsHeight = 0,
    List<string>? Classes = null,
    List<string>? AncestorTree = null,
    Dictionary<string, string>? Properties = null,
    bool IsHover = false,
    string? TreePath = null
) : ShowMeMessage;

public record ContextMenuHitTestRequestMessage(
    string RequestId,
    double X,
    double Y
) : ShowMeMessage;

public record VisualItemInfo(
    string TypeName,
    string? ElementName,
    int LineNumber,
    int LinePosition,
    string? SourceUri,
    double BoundsX = 0,
    double BoundsY = 0,
    double BoundsWidth = 0,
    double BoundsHeight = 0,
    string? TreePath = null
);

public record ContextMenuHitTestResponseMessage(
    string RequestId,
    double X,
    double Y,
    List<VisualItemInfo> Items
) : ShowMeMessage;

/// <summary>
/// Asks the preview for the element with this identity, wherever the layout has put it now. <see cref="TreePath"/>
/// tells apart the instances of a template that share the same source location.
/// </summary>
public record FindElementRequestMessage(
    string RequestId,
    string TypeName,
    string? ElementName,
    int LineNumber,
    int LinePosition,
    string? SourceUri,
    string? TreePath
) : ShowMeMessage;

/// <summary>
/// The element found for a <see cref="FindElementRequestMessage"/>, already scrolled into view, or null when it is not
/// in the preview.
/// </summary>
public record FindElementResponseMessage(
    string RequestId,
    VisualItemInfo? Item
) : ShowMeMessage;

public record XamlStatusMessage(
    bool Success,
    string? Error = null,
    int? LineNumber = null,
    int? LinePosition = null
) : ShowMeMessage;

public enum CatalogItemKindDto
{
    ControlTheme,
    Style,
    Brush,
    Color,
    Geometry,
    Template,
    Other
}

public record CatalogItemDto(
    string Id,
    string KeyOrSelector,
    CatalogItemKindDto Kind,
    string? GroupPath = null,
    string? ValueSummary = null
);

public record CatalogGroupDto(
    string Title,
    string Icon,
    string? OriginPath,
    List<CatalogItemDto> Items,
    List<CatalogGroupDto> Subgroups
);

public record CatalogInfoMessage(
    string Title,
    bool HasPreviewWith,
    CatalogGroupDto RootGroup,
    List<CatalogItemDto> AllItems
) : ShowMeMessage;

public record SelectCatalogItemMessage(
    string? ItemId,
    string ViewMode, // "All", "Single", "Group", "PreviewWith"
    string? FilterQuery = null
) : ShowMeMessage;

