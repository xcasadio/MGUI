using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Editor;
using MGUI.Editor.Document;
using MGUI.Editor.Properties;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Editor;

/// <summary><see cref="XamlNodePropertySource"/>: describes the selected XAML document node to <see cref="MGPropertyGrid"/>
/// (Docs/Tasks/xaml-editor-tasks.md, section X5; Docs/editor-architecture.md, "## Grille de proprietes"). One test group per
/// acceptance bullet of that section.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class XamlNodePropertySourceTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, XamlEditorView View, MGDockHost Host) CreateHostedView(int width, int height)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, width, height));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, width, height)
        {
            WindowStyle = WindowStyle.None,
        };

        XamlEditorSession session = new();
        XamlEditorView view = new(window, session);

        MGDockHost host = view.CreateDockHost();
        window.SetContent(host);
        desktop.Windows.Add(window);

        Frame(runtime, desktop, 0);
        Frame(runtime, desktop, 1);

        return (runtime, desktop, window, view, host);
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex)
    {
        Point p = new(1, 1);
        MouseState mouse = new(p.X, p.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    private static void SetTextAndSettle(GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view, string text, ref int frameIndex)
    {
        view.Session.Text = text;
        Frame(runtime, desktop, frameIndex += 20); // past the preview's 250 ms debounce
        Frame(runtime, desktop, frameIndex += 1);
    }

    /// <summary>Selects the (only) document node named <paramref name="localName"/> and returns it. Selection happens
    /// through the same public path the tree and the preview click use (<see cref="MGUI.Editor.Selection.XamlEditorSelection.SelectByOrdinal"/>).</summary>
    private static XamlDocumentNode SelectNode(XamlEditorView view, string localName)
    {
        XamlDocumentNode node = view.Selection.DocumentModel.Nodes.First(n => n.LocalName == localName && n.Ordinal.HasValue);
        view.Selection.SelectByOrdinal(node.Ordinal);
        return node;
    }

    // -- Reflection helpers into MGPropertyGrid's private view (same recipe as MGUI.Tests/Integration/PropertyGridTests.cs) --

    private static IEnumerable GetCategoryViews(MGPropertyGrid grid)
        => (IEnumerable)typeof(MGPropertyGrid).GetField("_CategoryViews", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(grid)!;

    private static bool TryGetRow(MGPropertyGrid grid, string descriptorName, out object row, out MGPropertyGridDescriptor descriptor)
    {
        foreach (object categoryView in GetCategoryViews(grid))
        {
            IList rows = (IList)categoryView.GetType().GetProperty("Rows", BindingFlags.Public | BindingFlags.Instance)!.GetValue(categoryView)!;
            foreach (object candidate in rows)
            {
                var candidateDescriptor = (MGPropertyGridDescriptor)candidate.GetType().GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Instance)!.GetValue(candidate)!;
                if (string.Equals(candidateDescriptor.Name, descriptorName, StringComparison.Ordinal))
                {
                    row = candidate;
                    descriptor = candidateDescriptor;
                    return true;
                }
            }
        }

        row = null;
        descriptor = null;
        return false;
    }

    private static object GetRow(MGPropertyGrid grid, string descriptorName)
    {
        Assert.True(TryGetRow(grid, descriptorName, out object row, out _), $"No row was found for descriptor '{descriptorName}'.");
        return row;
    }

    private static string GetRowDisplayText(object row)
    {
        object editor = row.GetType().GetProperty("Editor", BindingFlags.Public | BindingFlags.Instance)!.GetValue(row)!;
        var displayTextField = editor.GetType().GetField("DisplayText", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Editor '{editor.GetType().Name}' has no 'DisplayText' field: it is not a read-only row.");
        MGTextBlock displayText = (MGTextBlock)displayTextField.GetValue(editor)!;
        return displayText.Text;
    }

    private static MGBorder GetRowRoot(object row)
        => (MGBorder)row.GetType().GetProperty("Root", BindingFlags.Public | BindingFlags.Instance)!.GetValue(row)!;

    /// <summary>Advances frames until <paramref name="descriptorName"/>'s row has non-empty layout bounds (a live pane
    /// needs at least one layout pass), so an assertion against <see cref="MGPropertyGrid.RefreshVisibleValues"/>'s
    /// viewport-gated refresh is not racing the layout system.</summary>
    private static void SettleUntilRowHasBounds(GraphTestRuntime runtime, MGDesktop desktop, MGPropertyGrid grid, string descriptorName, ref int frame)
    {
        for (var i = 0; i < 20; i++)
        {
            Frame(runtime, desktop, ++frame);
            if (TryGetRow(grid, descriptorName, out object row, out _))
            {
                Rectangle bounds = GetRowRoot(row).ActualLayoutBounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    return;
                }
            }
        }
    }

    // -- 1. Descriptors of a Button node are present, announce typeof(string), carry the right Category, and really
    //       become a row of the grid; Children and AttachedProperties are absent --

    [Fact]
    public void ButtonDescriptors_AnnounceString_CarryTheirCategory_AndBecomeGridRows()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<StackPanel xmlns=\"" + Ns + "\" Name=\"Stack\">" +
            "<Button Name=\"Btn\" Width=\"120\" Margin=\"4\" HorizontalAlignment=\"Left\" Content=\"OK\" />" +
            "</StackPanel>",
            ref frame);

        SelectNode(view, "Button");

        // Every row that exists at all proves the grid did not silently drop it (the whole point of announcing
        // typeof(string)): a descriptor whose PropertyType leaked its real type (int?, Thickness?, an enum) would
        // never reach TryGetRow at all.
        Assert.Equal("Layout", TryGetRow(view.PropertyPane, "Width", out _, out var width) ? width.Category : null);
        Assert.Equal(typeof(string), width?.PropertyType);
        Assert.Equal("Layout", TryGetRow(view.PropertyPane, "Margin", out _, out var margin) ? margin.Category : null);
        Assert.Equal("Layout", TryGetRow(view.PropertyPane, "HorizontalAlignment", out _, out var horizontalAlignment) ? horizontalAlignment.Category : null);
        Assert.Equal("Misc", TryGetRow(view.PropertyPane, "Name", out _, out var name) ? name.Category : null);
        Assert.Equal("Data", TryGetRow(view.PropertyPane, "Content", out _, out var content) ? content.Category : null);

        Assert.False(TryGetRow(view.PropertyPane, "AttachedProperties", out _, out _));

        SelectNode(view, "StackPanel");
        Assert.False(TryGetRow(view.PropertyPane, "Children", out _, out _));
        Assert.False(TryGetRow(view.PropertyPane, "AttachedProperties", out _, out _));
    }

    // -- 2. Values: a declared attribute shows its written string; an absent attribute is empty, never null;
    //       Content="OK" is editable; Content declared as a child element is not --

    [Fact]
    public void DeclaredAttribute_ShowsItsWrittenString_AbsentAttribute_IsEmptyNeverNull()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view, "<Button xmlns=\"" + Ns + "\" Content=\"OK\" />", ref frame);
        SelectNode(view, "Button");

        Assert.Equal("OK", GetRowDisplayText(GetRow(view.PropertyPane, "Content")));
        Assert.Equal(string.Empty, GetRowDisplayText(GetRow(view.PropertyPane, "Margin")));

        var source = Assert.IsType<XamlNodePropertySource>(view.PropertyPane.SelectedObject);
        Assert.True(source.IsEditable("Content"));
    }

    [Fact]
    public void ContentDeclaredAsAChildElement_IsPermanentlyNotEditable()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        // Explicit property-element syntax.
        SetTextAndSettle(runtime, desktop, view,
            "<Button xmlns=\"" + Ns + "\"><Button.Content><TextBlock Text=\"Hi\" /></Button.Content></Button>",
            ref frame);
        SelectNode(view, "Button");
        var explicitSource = Assert.IsType<XamlNodePropertySource>(view.PropertyPane.SelectedObject);
        Assert.False(explicitSource.IsEditable("Content"));

        // Content supplied as a plain child object element, through the DTO's content property.
        SetTextAndSettle(runtime, desktop, view,
            "<Button xmlns=\"" + Ns + "\"><TextBlock Text=\"Hi\" /></Button>",
            ref frame);
        SelectNode(view, "Button");
        var implicitSource = Assert.IsType<XamlNodePropertySource>(view.PropertyPane.SelectedObject);
        Assert.False(implicitSource.IsEditable("Content"));
    }

    // -- 3. Every row is read-only in this slice --

    [Fact]
    public void EveryRow_IsReadOnly()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view, "<Button xmlns=\"" + Ns + "\" Content=\"OK\" Width=\"10\" />", ref frame);
        SelectNode(view, "Button");

        Assert.True(TryGetRow(view.PropertyPane, "Width", out _, out var width));
        Assert.True(width.IsReadOnly);
        Assert.True(TryGetRow(view.PropertyPane, "Content", out _, out var content));
        Assert.True(content.IsReadOnly);
    }

    // -- 4. Changing the selection rebuilds the grid: a different instance, a row of the previous node gone, a row of
    //       the new one present --

    [Fact]
    public void ChangingTheSelection_RebuildsTheGrid_WithANewSourceInstance()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<StackPanel xmlns=\"" + Ns + "\">" +
            "<Button Name=\"A\" BorderThickness=\"1\" />" +
            "<CheckBox Name=\"B\" IsCheckMarkShadowed=\"true\" />" +
            "</StackPanel>",
            ref frame);

        SelectNode(view, "Button");
        object firstSource = view.PropertyPane.SelectedObject;
        Assert.True(TryGetRow(view.PropertyPane, "BorderThickness", out _, out _));
        Assert.False(TryGetRow(view.PropertyPane, "IsCheckMarkShadowed", out _, out _));

        SelectNode(view, "CheckBox");
        object secondSource = view.PropertyPane.SelectedObject;

        Assert.NotSame(firstSource, secondSource);
        Assert.True(TryGetRow(view.PropertyPane, "IsCheckMarkShadowed", out _, out _));
        Assert.False(TryGetRow(view.PropertyPane, "BorderThickness", out _, out _));
    }

    // -- 5. A re-parse that keeps the selected node refreshes the values without rebuilding: the same SelectedObject
    //       instance, the row's displayed value changed --

    [Fact]
    public void AReparseThatKeepsTheSelectedNode_RefreshesValues_WithoutRebuilding()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        // BorderThickness is one of the first rows built (the "Border" category comes first): it is inside the
        // property grid's default (unscrolled) viewport, which RefreshVisibleValues needs in order to update it.
        SetTextAndSettle(runtime, desktop, view, "<Button xmlns=\"" + Ns + "\" BorderThickness=\"1\" />", ref frame);
        SelectNode(view, "Button");
        SettleUntilRowHasBounds(runtime, desktop, view.PropertyPane, "BorderThickness", ref frame);

        object source = view.PropertyPane.SelectedObject;
        Assert.Equal("1", GetRowDisplayText(GetRow(view.PropertyPane, "BorderThickness")));

        view.Session.Text = "<Button xmlns=\"" + Ns + "\" BorderThickness=\"2\" />";
        Frame(runtime, desktop, ++frame);

        Assert.Same(source, view.PropertyPane.SelectedObject);
        Assert.Equal("2", GetRowDisplayText(GetRow(view.PropertyPane, "BorderThickness")));
    }

    // -- 6. Hidden pane: a re-parse that changes a value while "Properties" is an inactive tab is visible in the grid
    //       as soon as the tab becomes active again --

    [Fact]
    public void HiddenPane_ShowsTheReparsedValue_AsSoonAsItBecomesActiveAgain()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view, "<Button xmlns=\"" + Ns + "\" BorderThickness=\"1\" />", ref frame);
        SelectNode(view, "Button");
        SettleUntilRowHasBounds(runtime, desktop, view.PropertyPane, "BorderThickness", ref frame);
        Assert.Equal("1", GetRowDisplayText(GetRow(view.PropertyPane, "BorderThickness")));

        // Merge "Properties" as a tab of "Document", then switch to "Document": "Properties" becomes the inactive tab
        // and, per the plan, its pane is detached (Docs/editor-architecture.md; MGUI.Tests/Editor/XamlPreviewHostTests.cs
        // "HiddenPane_..." is the precedent this follows).
        DockPanelNode propertiesPanel = host.FindPanel(XamlEditorView.PropertiesDockableId);
        DockTabGroupNode treeGroup = host.GetAllTabGroups().Single(group => group.Panels.Any(panel => panel.Id == XamlEditorView.TreeDockableId));
        DockOperation.DockAsTab(host.LayoutModel, propertiesPanel, treeGroup);
        treeGroup.SetActivePanel(XamlEditorView.TreeDockableId);
        Frame(runtime, desktop, ++frame);
        Assert.Null(view.PropertyPane.Parent);

        // A re-parse happens while the pane is hidden.
        view.Session.Text = "<Button xmlns=\"" + Ns + "\" BorderThickness=\"9\" />";
        Frame(runtime, desktop, ++frame);

        // Reactivating the tab re-attaches the pane; RefreshVisibleValues (from OnParentChanged) catches up.
        treeGroup.SetActivePanel(XamlEditorView.PropertiesDockableId);
        for (var i = 0; i < 5; i++)
        {
            Frame(runtime, desktop, ++frame);
        }

        Assert.NotNull(view.PropertyPane.Parent);
        Assert.Equal("9", GetRowDisplayText(GetRow(view.PropertyPane, "BorderThickness")));
    }

    // -- 7. "Resolved (runtime)": a Padding coming from an implicit style shows the effective value and a source that
    //       reads as an implicit style; no representative element -> the category is absent; a Resolved row does not
    //       collide with a DTO row of the same path (Padding) --

    [Fact]
    public void ResolvedCategory_ShowsTheImplicitStyleSource_AndCoexistsWithTheDtoRowOfTheSamePath()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"200\" Height=\"150\">" +
            "<Window.Styles><Style TargetType=\"Border\"><Setter Property=\"Padding\" Value=\"7\" /></Style></Window.Styles>" +
            "<Border Name=\"P\" />" +
            "</Window>",
            ref frame);

        SelectNode(view, "Border");
        Assert.NotNull(view.Selection.SelectedElement); // test setup: the representative resolved.

        Assert.Equal(string.Empty, GetRowDisplayText(GetRow(view.PropertyPane, "Padding")));

        string resolvedText = GetRowDisplayText(GetRow(view.PropertyPane, "Resolved.Padding"));
        Assert.Contains("7", resolvedText);
        Assert.Contains("ImplicitStyle", resolvedText);

        Assert.True(TryGetRow(view.PropertyPane, "Resolved.Padding", out _, out var resolvedDescriptor));
        Assert.Equal("Resolved (runtime)", resolvedDescriptor.Category);
        Assert.True(resolvedDescriptor.IsReadOnly);
    }

    [Fact]
    public void NoRepresentativeElement_TheResolvedCategoryIsAbsentEntirely()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        // Set the text and select immediately, before the debounced preview ever renders: the document model parses
        // synchronously, but no preview element exists yet, so the representative cannot resolve.
        view.Session.Text = "<Button xmlns=\"" + Ns + "\" Width=\"10\" />";
        XamlDocumentNode node = view.Selection.DocumentModel.Nodes.First(n => n.LocalName == "Button");
        view.Selection.SelectByOrdinal(node.Ordinal);

        Assert.Null(view.Selection.SelectedElement); // test setup: no representative yet.
        var source = Assert.IsType<XamlNodePropertySource>(view.PropertyPane.SelectedObject);
        Assert.Null(source.RepresentativeElement);

        Assert.True(TryGetRow(view.PropertyPane, "Width", out _, out _));
        AssertNoResolvedCategory(view.PropertyPane);
    }

    /// <summary>The way the category is actually reached while typing: the caret selects a node the instant it is typed, well
    /// before the debounced preview has rendered it, so the source is built without a representative element. When the preview
    /// arrives the rows must grow the "Resolved (runtime)" category -- which means a real rebuild, since a source never reshapes
    /// its own descriptor collection.</summary>
    [Fact]
    public void ARepresentativeThatResolvesAfterTheSelection_GrowsTheResolvedCategory()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        view.Session.Text = "<Button xmlns=\"" + Ns + "\" Width=\"10\" />";
        XamlDocumentNode node = view.Selection.DocumentModel.Nodes.First(n => n.LocalName == "Button");
        view.Selection.SelectByOrdinal(node.Ordinal);

        Assert.Null(view.Selection.SelectedElement); // test setup: selected before the preview rendered.
        AssertNoResolvedCategory(view.PropertyPane);

        Frame(runtime, desktop, frame += 20); // past the preview's 250 ms debounce
        Frame(runtime, desktop, frame += 1);

        Assert.NotNull(view.Selection.SelectedElement);
        Assert.True(TryGetRow(view.PropertyPane, "Resolved.Padding", out _, out var resolvedDescriptor),
            "The category must appear once the representative element resolves, without the author having to reselect the node.");
        Assert.Equal("Resolved (runtime)", resolvedDescriptor.Category);
        Assert.True(TryGetRow(view.PropertyPane, "Width", out _, out _));
    }

    /// <summary>A node keeps its ordinal when its tag is rewritten, so the selection survives -- but the rows come from the DTO
    /// type, which just changed.</summary>
    [Fact]
    public void ARewrittenTagAtTheSelectedOrdinal_ReplacesTheRowsWithTheNewTypes()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view, "<Button xmlns=\"" + Ns + "\" BorderThickness=\"1\" />", ref frame);
        SelectNode(view, "Button");
        Assert.True(TryGetRow(view.PropertyPane, "BorderThickness", out _, out _));

        SetTextAndSettle(runtime, desktop, view, "<CheckBox xmlns=\"" + Ns + "\" IsCheckMarkShadowed=\"true\" />", ref frame);

        Assert.Equal("CheckBox", view.Selection.SelectedNode.LocalName);
        Assert.True(TryGetRow(view.PropertyPane, "IsCheckMarkShadowed", out _, out _),
            "The rows must describe the node's current DTO type, not the type it had when the source was built.");
        Assert.False(TryGetRow(view.PropertyPane, "IsRepeatButton", out _, out _),
            "A property of the previous type must be gone.");
    }

    private static void AssertNoResolvedCategory(MGPropertyGrid grid)
    {
        foreach (object categoryView in GetCategoryViews(grid))
        {
            string categoryName = (string)categoryView.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)!.GetValue(categoryView)!;
            Assert.NotEqual("Resolved (runtime)", categoryName);
        }
    }
}
