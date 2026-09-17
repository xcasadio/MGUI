using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;

namespace MGUI.Editor;

/// <summary>Composes the empty shell of the XAML editor: a three-column grid, separated by two <see cref="MGGridSplitter"/>,
/// holding a text pane, a preview pane and a right-hand column with a tree pane above a property pane.<para/>
/// This is a plain composing class, not an <see cref="MGElement"/>: it builds its elements against the given
/// <see cref="MGWindow"/> but does not set the window's content and does not add the window to a desktop; the host does.<para/>
/// No pane has any behaviour in this slice: no text binding, no preview, no selection, no subscribed events.</summary>
public class XamlEditorView
{
    /// <summary>The <see cref="MGElement.Name"/> given to <see cref="TextPane"/>.</summary>
    public const string TextPaneName = "TextPane";
    /// <summary>The <see cref="MGElement.Name"/> given to <see cref="PreviewPane"/>.</summary>
    public const string PreviewPaneName = "PreviewPane";
    /// <summary>The <see cref="MGElement.Name"/> given to <see cref="TreePane"/>.</summary>
    public const string TreePaneName = "TreePane";
    /// <summary>The <see cref="MGElement.Name"/> given to <see cref="PropertyPane"/>.</summary>
    public const string PropertyPaneName = "PropertyPane";

    /// <summary>The window every element of this view was constructed against.</summary>
    public MGWindow Window { get; }
    /// <summary>The session this view was built for.</summary>
    public XamlEditorSession Session { get; }

    /// <summary>The root grid: one row, three content columns (text, preview, right-hand tree/property column)
    /// separated by two <see cref="MGGridSplitter"/>.</summary>
    public MGGrid Root { get; }

    /// <summary>The left column: the text editor for the raw XAML markup.</summary>
    public MGRichTextBox TextPane { get; }
    /// <summary>The centre column: an overlay panel that will host the rendered preview, and later the adorner layer.</summary>
    public MGOverlayPanel PreviewPane { get; }
    /// <summary>The content presenter, inside <see cref="PreviewPane"/>, that will host the previewed content.</summary>
    public MGContentPresenter PreviewPresenter { get; }
    /// <summary>The right column, top row: the visual tree of the previewed document.</summary>
    public MGTreeView TreePane { get; }
    /// <summary>The right column, bottom row: the property grid for the current selection.</summary>
    public MGPropertyGrid PropertyPane { get; }

    /// <summary>The pixel width of each <see cref="MGGridSplitter"/> column, matching <see cref="MGGridSplitter.Size"/>'s default.</summary>
    private const int SplitterColumnWidth = 12;

    public XamlEditorView(MGWindow window, XamlEditorSession session)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Session = session ?? throw new ArgumentNullException(nameof(session));

        Root = new MGGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Root.AddRow(GridLength.CreateWeightedLength(1));
        Root.AddColumn(GridLength.CreateWeightedLength(1));
        Root.AddColumn(GridLength.CreatePixelLength(SplitterColumnWidth));
        Root.AddColumn(GridLength.CreateWeightedLength(1));
        Root.AddColumn(GridLength.CreatePixelLength(SplitterColumnWidth));
        Root.AddColumn(GridLength.CreateWeightedLength(1));

        TextPane = new MGRichTextBox(window)
        {
            Name = TextPaneName,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Root.TryAddChild(0, 0, TextPane);

        MGGridSplitter leftSplitter = new(window);
        Root.TryAddChild(0, 1, leftSplitter);

        PreviewPresenter = new MGContentPresenter(window);
        PreviewPane = new MGOverlayPanel(window)
        {
            Name = PreviewPaneName,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        PreviewPane.TryAddChild(PreviewPresenter);
        Root.TryAddChild(0, 2, PreviewPane);

        MGGridSplitter rightSplitter = new(window);
        Root.TryAddChild(0, 3, rightSplitter);

        MGGrid rightColumn = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        rightColumn.AddColumn(GridLength.CreateWeightedLength(1));
        rightColumn.AddRow(GridLength.CreateWeightedLength(1));
        rightColumn.AddRow(GridLength.CreateWeightedLength(1));

        TreePane = new MGTreeView(window)
        {
            Name = TreePaneName,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        rightColumn.TryAddChild(0, 0, TreePane);

        PropertyPane = new MGPropertyGrid(window)
        {
            Name = PropertyPaneName,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        rightColumn.TryAddChild(1, 0, PropertyPane);

        Root.TryAddChild(0, 4, rightColumn);
    }
}
