using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using Zafiro.Avalonia.ShowMe.ViewModels;

namespace Zafiro.Avalonia.ShowMe.Views;

/// <summary>
/// Read-only XAML viewer with XML highlighting that scrolls to the element's line and highlights it.
/// </summary>
public sealed class XamlSourceView : UserControl
{
    public static readonly StyledProperty<XamlSourceViewModel?> SourceProperty =
        AvaloniaProperty.Register<XamlSourceView, XamlSourceViewModel?>(nameof(Source));

    // The built-in XML colors are meant for light backgrounds; these keep the same roles readable on dark ones.
    private static readonly Dictionary<string, Color> DarkXmlColors = new()
    {
        ["Comment"] = Color.Parse("#6A9955"),
        ["CData"] = Color.Parse("#CE9178"),
        ["DocType"] = Color.Parse("#569CD6"),
        ["XmlDeclaration"] = Color.Parse("#569CD6"),
        ["XmlTag"] = Color.Parse("#569CD6"),
        ["AttributeName"] = Color.Parse("#9CDCFE"),
        ["AttributeValue"] = Color.Parse("#CE9178"),
        ["Entity"] = Color.Parse("#D7BA7D"),
        ["BrokenEntity"] = Color.Parse("#F44747"),
    };

    private static readonly Lazy<IHighlightingDefinition> DarkXml = new(LoadDarkXml);

    private readonly TextEditor editor;
    private readonly LineHighlighter highlighter = new();

    public XamlSourceView()
    {
        editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = true,
            FontFamily = new FontFamily("Cascadia Code,JetBrains Mono,Consolas,DejaVu Sans Mono,monospace"),
            FontSize = 12.5,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        editor.TextArea.TextView.BackgroundRenderers.Add(highlighter);
        Content = editor;
        ApplyHighlighting();
        ActualThemeVariantChanged += (_, _) => ApplyHighlighting();
    }

    public XamlSourceViewModel? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            Show(Source);
        }
    }

    private void Show(XamlSourceViewModel? source)
    {
        editor.Text = source?.Text ?? "";
        highlighter.Line = source?.LineNumber ?? 0;
        editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

        if (source == null || source.LineNumber <= 0 || source.LineNumber > editor.Document.LineCount)
        {
            return;
        }

        var line = source.LineNumber;
        var column = Math.Max(source.LinePosition, 1);
        editor.TextArea.Caret.Line = line;
        editor.TextArea.Caret.Column = column;

        // Scrolling needs the editor laid out, which has not happened yet when the viewer has just been opened.
        Dispatcher.UIThread.Post(() => editor.ScrollTo(line, column), DispatcherPriority.Loaded);
    }

    private void ApplyHighlighting()
    {
        editor.SyntaxHighlighting = ActualThemeVariant == ThemeVariant.Dark
            ? DarkXml.Value
            : HighlightingManager.Instance.GetDefinition("XML");
    }

    private static IHighlightingDefinition LoadDarkXml()
    {
        using var stream = typeof(TextEditor).Assembly.GetManifestResourceStream("AvaloniaEdit.Highlighting.Resources.XML-Mode.xshd")!;
        using var reader = System.Xml.XmlReader.Create(stream);
        var definition = HighlightingLoader.LoadXshd(reader);
        foreach (var color in definition.Elements.OfType<XshdColor>())
        {
            if (color.Name != null && DarkXmlColors.TryGetValue(color.Name, out var dark))
            {
                color.Foreground = new SimpleHighlightingBrush(dark);
            }
        }

        return HighlightingLoader.Load(definition, HighlightingManager.Instance);
    }

    private sealed class LineHighlighter : IBackgroundRenderer
    {
        private static readonly IBrush Fill = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x78, 0xD4));
        private static readonly IBrush Marker = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));

        public int Line { get; set; }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView.Document == null || Line <= 0 || Line > textView.Document.LineCount)
            {
                return;
            }

            var documentLine = textView.Document.GetLineByNumber(Line);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, documentLine, extendToFullWidthAtLineEnd: true))
            {
                drawingContext.FillRectangle(Fill, new Rect(0, rect.Y, textView.Bounds.Width, rect.Height));
                drawingContext.FillRectangle(Marker, new Rect(0, rect.Y, 3, rect.Height));
            }
        }
    }
}
