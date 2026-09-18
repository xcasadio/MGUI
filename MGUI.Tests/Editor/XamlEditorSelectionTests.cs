using System;
using System.Linq;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.Editor;
using MGUI.Editor.Document;
using MGUI.Editor.Selection;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Editor;

/// <summary><see cref="XamlEditorSelection"/>: the single selection shared by the document tree, a click in the
/// non-interactive preview, and the text caret (Docs/Tasks/xaml-editor-tasks.md, section X4;
/// Docs/editor-architecture.md, "## Selection"). One test group per acceptance bullet of that section.</summary>
public class XamlEditorSelectionTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, XamlEditorView View) CreateHostedView(int width, int height)
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

        return (runtime, desktop, window, view);
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex, Point? position = null, MouseButton? pressedButton = null)
    {
        Point p = position ?? new Point(1, 1);
        MouseState mouse = new(p.X, p.Y, 0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    /// <summary>A full press-release cycle at <paramref name="position"/>, close enough in time to register as one click
    /// on whatever <see cref="MGUI.Shared.Input.Mouse.MouseHandler"/> owns that screen position.</summary>
    private static void Click(GraphTestRuntime runtime, MGDesktop desktop, ref int frameIndex, Point position)
    {
        Frame(runtime, desktop, ++frameIndex, position);
        Frame(runtime, desktop, ++frameIndex, position, MouseButton.Left);
        Frame(runtime, desktop, ++frameIndex, position);
    }

    private static void SetTextAndSettle(GraphTestRuntime runtime, MGDesktop desktop, XamlEditorView view, string text, ref int frameIndex)
    {
        view.Session.Text = text;
        Frame(runtime, desktop, frameIndex += 20); // past the preview's 250 ms debounce
        Frame(runtime, desktop, frameIndex += 1);
    }

    // -- 1. Walk-up: a click on a template part, on an inner control's own component, and on a TypeConverter-produced
    //       label all select the declared node that owns them --

    [Fact]
    public void ClickingATemplatePart_SelectsTheDeclaredNodeThatOwnsIt()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<NumericUpDown xmlns=\"" + Ns + "\" Name=\"Spinner\" Width=\"120\" Height=\"32\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />",
            ref frame);

        MGElement previewRoot = view.PreviewHost.PreviewRoot;
        Assert.NotNull(previewRoot);
        Assert.True(UIToolingService.TryGetXamlSourcePosition(previewRoot, out XamlSourcePosition rootPosition));

        Assert.True(((MGNumericUpDown)previewRoot).TryGetTemplatePart(MGNumericUpDown.IncreaseButtonPartName, out MGElement increaseButton));
        Assert.False(UIToolingService.TryGetXamlSourcePosition(increaseButton, out _), "Test setup: a template part must not itself carry a source position.");

        Click(runtime, desktop, ref frame, increaseButton.ActualLayoutBounds.Center);

        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(rootPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
        Assert.Equal("NumericUpDown", view.Selection.SelectedNode.LocalName);
    }

    /// <summary>The same walk-up on a template part of a different control, and the one the author actually clicks: the preview
    /// root is a <c>Window</c>, so its chrome is on screen and the title bar is a part of a control other than the one whose own
    /// buttons the test above clicks.</summary>
    [Fact]
    public void ClickingTheTitleBarPartOfAPreviewedWindow_SelectsTheWindowNode()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"240\" Height=\"140\" TitleText=\"Preview\" />",
            ref frame);

        MGElement previewRoot = view.PreviewHost.PreviewRoot;
        Assert.NotNull(previewRoot);
        Assert.True(UIToolingService.TryGetXamlSourcePosition(previewRoot, out XamlSourcePosition windowPosition));

        Assert.True(((MGWindow)previewRoot).TryGetTemplatePart(MGWindow.TitleBarPartName, out MGElement titleBar));
        Assert.False(UIToolingService.TryGetXamlSourcePosition(titleBar, out _), "Test setup: a template part must not itself carry a source position.");
        Assert.True(titleBar.ActualLayoutBounds.Width > 0 && titleBar.ActualLayoutBounds.Height > 0, "Test setup: the title bar must be on screen.");

        Click(runtime, desktop, ref frame, titleBar.ActualLayoutBounds.Center);

        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(windowPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
        Assert.Equal("Window", view.Selection.SelectedNode.LocalName);
    }

    [Fact]
    public void ClickingTheLabelOfAButtonDeclaredByStringContent_SelectsTheButton()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Button xmlns=\"" + Ns + "\" Content=\"OK\" Width=\"80\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />",
            ref frame);

        MGElement previewRoot = view.PreviewHost.PreviewRoot;
        Assert.NotNull(previewRoot);
        Assert.True(UIToolingService.TryGetXamlSourcePosition(previewRoot, out XamlSourcePosition buttonPosition));

        MGTextBlock label = previewRoot.TraverseVisualTree(includeComponents: true).OfType<MGTextBlock>().First();
        Assert.False(UIToolingService.TryGetXamlSourcePosition(label, out _), "Test setup: a string-content label must not itself carry a source position.");

        Click(runtime, desktop, ref frame, label.ActualLayoutBounds.Center);

        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(buttonPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
        Assert.Equal("Button", view.Selection.SelectedNode.LocalName);
    }

    // -- 2. Preview click, tree selection and caret move give the same selection and update the other two, with no loop --

    [Fact]
    public void PreviewClick_TreeSelection_AndCaretMove_AgreeAndSyncWithoutLooping()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Window xmlns=\"" + Ns + "\" WindowStyle=\"None\" Left=\"0\" Top=\"0\" Width=\"300\" Height=\"200\">" +
            "<Button Name=\"OkButton\" Content=\"OK\" Width=\"80\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />" +
            "</Window>",
            ref frame);

        int changedCount = 0;
        view.Selection.Changed += (_, _) => changedCount++;

        MGWindow previewRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        MGButton button = previewRoot.GetElementByName<MGButton>("OkButton");
        Assert.NotNull(button);
        Assert.True(UIToolingService.TryGetXamlSourcePosition(button, out XamlSourcePosition buttonPosition));
        Assert.True(UIToolingService.TryGetXamlSourcePosition(previewRoot, out XamlSourcePosition windowPosition));

        // 1) A click in the preview selects the button and updates the tree and the caret.
        Click(runtime, desktop, ref frame, button.ActualLayoutBounds.Center);
        Assert.Equal(1, changedCount);
        Assert.Equal(buttonPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
        Assert.Same(button, view.Selection.SelectedElement);
        Assert.NotNull(view.TreePane.SelectedItem);
        Assert.Equal(buttonPosition.Ordinal, (int)view.TreePane.SelectedItem.Tag);
        Assert.Equal(view.Selection.SelectedNode.StartTagRange.StartIndex, view.TextPane.CaretIndex);

        // 2) Selecting the root item in the tree selects the window and updates the preview's adorner and the caret.
        Assert.True(view.Selection.DocumentModel.TryGetNodeByOrdinal(windowPosition.Ordinal, out XamlDocumentNode windowNode));
        MGTreeViewItem windowItem = view.TreePane.Items.Single(i => (int)i.Tag == windowNode.Ordinal);
        view.TreePane.SelectItem(windowItem);
        Assert.Equal(2, changedCount);
        Assert.Equal(windowPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
        Assert.Equal(view.Selection.SelectedNode.StartTagRange.StartIndex, view.TextPane.CaretIndex);

        // 3) Selecting the same tree item again changes nothing and raises nothing.
        view.TreePane.SelectItem(windowItem);
        Assert.Equal(2, changedCount);

        // 4) A click back on the button reaches the same selection again, still with exactly one more event.
        Click(runtime, desktop, ref frame, button.ActualLayoutBounds.Center);
        Assert.Equal(3, changedCount);
        Assert.Equal(buttonPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
    }

    // -- 3. A caret move produced by real input (a click) changes the selection after one tick; a tick without a move raises nothing --

    [Fact]
    public void ACaretMoveProducedByInput_ChangesTheSelection_ATickWithoutAMoveDoesNot()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Button xmlns=\"" + Ns + "\" Content=\"OK\" Width=\"80\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />",
            ref frame);

        view.TextPane.ShowLineNumbers = false;
        Assert.Null(view.Selection.SelectedNode);

        int changedCount = 0;
        view.Selection.Changed += (_, _) => changedCount++;

        // A real click inside the text pane, never a direct call to SelectByCaret nor to the CaretIndex setter.
        Point clickPoint = new(view.TextPane.ActualLayoutBounds.X + 5, view.TextPane.ActualLayoutBounds.Y + 5);
        Click(runtime, desktop, ref frame, clickPoint);
        Frame(runtime, desktop, ++frame); // the polling tick that must notice the caret moved

        Assert.True(view.TextPane.Caret.HasPosition);
        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(1, changedCount);

        // A further tick with no new caret move raises nothing.
        Frame(runtime, desktop, ++frame);
        Assert.Equal(1, changedCount);
    }

    // -- 4. A node generated by an item template maps to many elements: the click carries which one is the representative --

    [Fact]
    public void AListItemTemplateClick_SelectsTheSharedNode_AndAdornsOnlyTheClickedItem()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<ListBox xmlns=\"" + Ns + "\" Name=\"MyListBox\" Width=\"200\" Height=\"200\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">" +
            "<ListBox.ItemTemplate><ContentTemplate><StackPanel Orientation=\"Horizontal\"><TextBlock Name=\"ItemLabel\" Text=\"Item\" /></StackPanel></ContentTemplate></ListBox.ItemTemplate>" +
            "<TextBlock Text=\"Option 1\" /><TextBlock Text=\"Option 2\" /><TextBlock Text=\"Option 3\" />" +
            "</ListBox>",
            ref frame);

        var listBox = Assert.IsType<MGListBox<object>>(view.PreviewHost.PreviewRoot);
        Assert.Equal(3, listBox.ListBoxItems.Count);

        MGElement thirdItemLabel = listBox.ListBoxItems[2].Content.TraverseVisualTree(includeComponents: true).OfType<MGTextBlock>().First();
        MGElement firstItemLabel = listBox.ListBoxItems[0].Content.TraverseVisualTree(includeComponents: true).OfType<MGTextBlock>().First();
        Assert.True(UIToolingService.TryGetXamlSourcePosition(thirdItemLabel, out XamlSourcePosition thirdPosition));
        Assert.True(UIToolingService.TryGetXamlSourcePosition(firstItemLabel, out XamlSourcePosition firstPosition));
        Assert.Equal(firstPosition.Ordinal, thirdPosition.Ordinal); // one template node, generated three times

        Click(runtime, desktop, ref frame, thirdItemLabel.ActualLayoutBounds.Center);

        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(thirdPosition.Ordinal, view.Selection.SelectedNode.Ordinal);
        // The representative is the exact element the click carried (the third item), not the first occurrence.
        Assert.Same(thirdItemLabel, view.Selection.SelectedElement);

        // The adorner is on exactly that one item: its bounds match the third label, not the first.
        MGUI.Core.UI.Adorners.MGBoundsAdorner adorner = GetAdorner(view.Selection);
        Assert.True(adorner.TryGetAdornedBounds(out Rectangle adornedBounds));
        Assert.Equal(thirdItemLabel.ActualLayoutBounds, adornedBounds);
        Assert.NotEqual(firstItemLabel.ActualLayoutBounds, adornedBounds);
    }

    private static MGUI.Core.UI.Adorners.MGBoundsAdorner GetAdorner(XamlEditorSelection selection)
    {
        var field = typeof(XamlEditorSelection).GetField("_adorner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (MGUI.Core.UI.Adorners.MGBoundsAdorner)field.GetValue(selection);
    }

    // -- 5. The adorner follows the selected element after a re-parse that changes its size --

    [Fact]
    public void TheAdorner_FollowsTheSelectedElement_AfterAReparseThatChangesItsSize()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Button xmlns=\"" + Ns + "\" Content=\"OK\" Width=\"80\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />",
            ref frame);

        MGElement firstRoot = view.PreviewHost.PreviewRoot;
        Assert.NotNull(firstRoot);
        Click(runtime, desktop, ref frame, firstRoot.ActualLayoutBounds.Center);
        Assert.Same(firstRoot, view.Selection.SelectedElement);

        MGUI.Core.UI.Adorners.MGBoundsAdorner adorner = GetAdorner(view.Selection);
        Assert.True(adorner.TryGetAdornedBounds(out Rectangle firstBounds));
        Assert.Equal(firstRoot.ActualLayoutBounds, firstBounds);

        SetTextAndSettle(runtime, desktop, view,
            "<Button xmlns=\"" + Ns + "\" Content=\"OK\" Width=\"160\" Height=\"60\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />",
            ref frame);
        Frame(runtime, desktop, frame += 2);

        MGElement secondRoot = view.PreviewHost.PreviewRoot;
        Assert.NotNull(secondRoot);
        Assert.NotSame(firstRoot, secondRoot);
        Assert.Same(secondRoot, view.Selection.SelectedElement);

        Assert.True(adorner.TryGetAdornedBounds(out Rectangle secondBounds));
        Assert.Equal(secondRoot.ActualLayoutBounds, secondBounds);
        Assert.NotEqual(firstBounds.Size, secondBounds.Size);
    }

    // -- 6. Survives a valid re-parse by ordinal; survives invalid XAML; cleared when the node disappears --

    [Fact]
    public void Selection_SurvivesAValidReparseByOrdinal_SurvivesInvalidXaml_AndIsClearedWhenTheNodeDisappears()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Window xmlns=\"" + Ns + "\" WindowStyle=\"None\" Left=\"0\" Top=\"0\" Width=\"300\" Height=\"200\">" +
            "<Button Name=\"OkButton\" Content=\"OK\" Width=\"80\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />" +
            "</Window>",
            ref frame);

        MGWindow previewRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        MGButton button = previewRoot.GetElementByName<MGButton>("OkButton");
        Assert.NotNull(button);
        Click(runtime, desktop, ref frame, button.ActualLayoutBounds.Center);
        Assert.NotNull(view.Selection.SelectedNode);
        int selectedOrdinal = view.Selection.SelectedNode.Ordinal.Value;

        int changedCount = 0;
        view.Selection.Changed += (_, _) => changedCount++;

        // A valid re-parse (attribute value changed, same node ordinal) keeps the selection, silently.
        view.Session.Text = "<Window xmlns=\"" + Ns + "\" WindowStyle=\"None\" Left=\"0\" Top=\"0\" Width=\"300\" Height=\"200\">" +
                             "<Button Name=\"OkButton\" Content=\"OK\" Width=\"90\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />" +
                             "</Window>";
        Frame(runtime, desktop, frame += 1);
        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(selectedOrdinal, view.Selection.SelectedNode.Ordinal);
        Assert.Equal(0, changedCount);

        // Invalid XAML: the model and the selection are both left exactly as they were.
        view.Session.Text = "<Window xmlns=\"" + Ns + "\"><Button";
        Frame(runtime, desktop, frame += 1);
        Assert.NotNull(view.Selection.SelectedNode);
        Assert.Equal(selectedOrdinal, view.Selection.SelectedNode.Ordinal);
        Assert.Equal(0, changedCount);

        // The node disappears entirely: the selection is cleared, and that is reported once.
        view.Session.Text = "<Window xmlns=\"" + Ns + "\" WindowStyle=\"None\" Left=\"0\" Top=\"0\" Width=\"300\" Height=\"200\"></Window>";
        Frame(runtime, desktop, frame += 1);
        Assert.Null(view.Selection.SelectedNode);
        Assert.Null(view.Selection.SelectedElement);
        Assert.Equal(1, changedCount);
    }

    // -- 7. The tree keeps its expansion state across a rebuild --

    [Fact]
    public void TheTree_KeepsItsExpansionState_AcrossARebuild()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view) = CreateHostedView(1280, 720);
        int frame = 0;

        SetTextAndSettle(runtime, desktop, view,
            "<Window xmlns=\"" + Ns + "\" WindowStyle=\"None\" Left=\"0\" Top=\"0\" Width=\"300\" Height=\"200\">" +
            "<StackPanel Name=\"Outer\"><Button Name=\"Inner\" Content=\"OK\" /></StackPanel>" +
            "</Window>",
            ref frame);

        // First parse: every item defaults to expanded.
        Assert.All(view.TreePane.Items, item => Assert.True(item.IsExpanded));
        MGTreeViewItem rootItem = view.TreePane.Items.Single();
        MGTreeViewItem outerItem = rootItem.Items.Single();
        Assert.True(outerItem.IsExpanded);
        outerItem.Collapse();
        Assert.False(outerItem.IsExpanded);

        // A re-parse that keeps the same nodes (an attribute value changed) rebuilds the tree, but the collapsed item
        // stays collapsed and the still-expanded ones stay expanded.
        view.Session.Text = "<Window xmlns=\"" + Ns + "\" WindowStyle=\"None\" Left=\"0\" Top=\"0\" Width=\"320\" Height=\"200\">" +
                             "<StackPanel Name=\"Outer\"><Button Name=\"Inner\" Content=\"OK\" /></StackPanel>" +
                             "</Window>";
        Frame(runtime, desktop, frame += 1);

        MGTreeViewItem rebuiltRootItem = view.TreePane.Items.Single();
        Assert.True(rebuiltRootItem.IsExpanded);
        MGTreeViewItem rebuiltOuterItem = rebuiltRootItem.Items.Single();
        Assert.False(rebuiltOuterItem.IsExpanded);
    }
}
