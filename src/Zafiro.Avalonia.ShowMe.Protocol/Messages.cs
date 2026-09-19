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
    string? XamlAssemblyPath = null
) : ShowMeMessage;

public record UpdateXamlMessage(
    string Xaml,
    string? Theme = null
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
    bool IsHover = false
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
    double BoundsHeight = 0
);

public record ContextMenuHitTestResponseMessage(
    string RequestId,
    double X,
    double Y,
    List<VisualItemInfo> Items
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

