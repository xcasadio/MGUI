using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.Core.Tooling;
using MGUI.Editor;
using MGUI.Editor.Preview;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Editor;

/// <summary><see cref="XamlPreviewHost"/> renders a <see cref="XamlEditorSession"/>'s text into the "Preview" pane,
/// debounced on the editor window's own update tick. One test group per acceptance bullet of the hot preview host
/// section of Docs/Tasks/xaml-editor-tasks.md.</summary>
public class XamlPreviewHostTests
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

    /// <summary>Advances the runtime's clock to <paramref name="frameIndex"/> * 16 ms and runs one desktop update, with
    /// the mouse at <paramref name="position"/> (default (1,1), off every pane) and <paramref name="pressedButton"/>
    /// held, if any.</summary>
    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex, Point? position = null, MouseButton? pressedButton = null)
    {
        Point p = position ?? new Point(1, 1);
        MouseState mouse = new(p.X, p.Y, 0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(System.TimeSpan.FromMilliseconds(16 * frameIndex), System.TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    // -- 1. Debounce: no re-parse before the delay, exactly one after a burst of changes --

    [Fact]
    public void RequestRefresh_Debounces_ABurstProducesExactlyOneReparse()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        XamlPreviewHost host = view.PreviewHost;
        int versionAfterSetup = host.PreviewVersion;
        int updatedCount = 0;
        host.PreviewUpdated += (_, _) => updatedCount++;

        // A burst of three edits within the debounce window (250 ms default).
        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"A\" />";
        Frame(runtime, desktop, 2);
        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"AB\" />";
        Frame(runtime, desktop, 3);
        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"ABC\" />";

        // Still well within 250 ms of the last edit: no re-parse yet.
        Frame(runtime, desktop, 10); // 160 ms since frame 3
        Assert.Equal(versionAfterSetup, host.PreviewVersion);
        Assert.Equal(0, updatedCount);

        // Past the debounce delay since the last edit in the burst: exactly one re-parse.
        Frame(runtime, desktop, 20); // 272 ms since frame 3
        Assert.Equal(versionAfterSetup + 1, host.PreviewVersion);
        Assert.Equal(1, updatedCount);
        Assert.NotNull(host.PreviewRoot);

        // No further re-parse without a new request.
        Frame(runtime, desktop, 40);
        Assert.Equal(versionAfterSetup + 1, host.PreviewVersion);
        Assert.Equal(1, updatedCount);
    }

    // -- 2. Invalid XAML: previous root kept, diagnostic with code, line and column --

    [Fact]
    public void InvalidXaml_KeepsThePreviousRoot_AndPublishesADiagnosticWithCodeLineAndColumn()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        XamlPreviewHost host = view.PreviewHost;

        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"Valid\" />";
        Frame(runtime, desktop, 20);
        MGElement validRoot = host.PreviewRoot;
        Assert.NotNull(validRoot);
        Assert.Empty(host.Diagnostics);

        view.Session.Text = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"10\" Height=\"10\">\n    <ThisElementDoesNotExist />\n</Window>";
        Frame(runtime, desktop, 40);

        Assert.Same(validRoot, host.PreviewRoot);
        Assert.Single(host.Diagnostics);
        XamlLoaderDiagnostic diagnostic = host.Diagnostics[0];
        Assert.Equal(XamlLoaderDiagnosticCode.UnknownType, diagnostic.Code);
        Assert.NotNull(diagnostic.LineNumber);
        Assert.NotNull(diagnostic.LinePosition);
        Assert.Equal(2, diagnostic.LineNumber);
    }

    // -- 3. DesignDataContext reaches DataContextOverride of the root --

    [Fact]
    public void DesignDataContext_ReachesDataContextOverride_OfTheRoot()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        XamlPreviewHost host = view.PreviewHost;

        object dataContext = new();
        view.Session.DesignDataContext = dataContext;
        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"Hi\" />";
        Frame(runtime, desktop, 20);

        Assert.NotNull(host.PreviewRoot);
        Assert.Same(dataContext, host.PreviewRoot.DataContextOverride);
    }

    // -- 4. Window root anchored to the pane, at the offset its document declares, re-anchored after the editor window moves and after the pane resizes --

    [Fact]
    public void WindowRoot_IsAnchoredToThePaneCorner_AndReAnchoredAfterWindowMoveAndPaneResize()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window, XamlEditorView view, _) = CreateHostedView(1280, 720);

        view.Session.Text = "<Window xmlns=\"" + Ns + "\" Left=\"440\" Top=\"20\" Width=\"200\" Height=\"150\"></Window>";
        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 21);

        MGWindow previewRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        Rectangle paneBounds = view.PreviewPane.LayoutBounds;
        // The pane's corner is the preview's origin; the document's Left and Top place the window from there.
        Assert.Equal(new Point(paneBounds.X + 440, paneBounds.Y + 20), previewRoot.TopLeft);

        // Move the editor window: no new keystroke, but the preview pane's screen bounds shift with it.
        window.Left = 50;
        window.Top = 30;
        Frame(runtime, desktop, 22);
        Frame(runtime, desktop, 23);

        Rectangle paneBoundsAfterMove = view.PreviewPane.LayoutBounds;
        Assert.NotEqual(paneBounds, paneBoundsAfterMove);
        Assert.Equal(new Point(paneBoundsAfterMove.X + 440, paneBoundsAfterMove.Y + 20), previewRoot.TopLeft);

        // Change the pane's width (a docking splitter drag): re-anchored again, no new keystroke.
        window.WindowWidth = 1000;
        Frame(runtime, desktop, 24);
        Frame(runtime, desktop, 25);

        Rectangle paneBoundsAfterResize = view.PreviewPane.LayoutBounds;
        Assert.NotEqual(paneBoundsAfterMove, paneBoundsAfterResize);
        Assert.Equal(new Point(paneBoundsAfterResize.X + 440, paneBoundsAfterResize.Y + 20), previewRoot.TopLeft);
    }

    // -- 5. A root bigger than the pane stays anchored and is not resized --

    [Fact]
    public void OversizedWindowRoot_StaysAnchored_AndIsNotResized()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        view.Session.Text = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"500\" Height=\"800\"></Window>";
        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 21);

        MGWindow previewRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        Rectangle paneBounds = view.PreviewPane.LayoutBounds;
        Assert.True(paneBounds.Width < 500 || paneBounds.Height < 800, "Test setup: the pane must be smaller than the declared root.");

        Assert.Equal(500, previewRoot.WindowWidth);
        Assert.Equal(800, previewRoot.WindowHeight);
        Assert.Equal(new Point(paneBounds.X, paneBounds.Y), previewRoot.TopLeft);
    }

    // -- 6. Hidden pane: root hidden, re-parse still happens; reactivated tab: root visible and anchored, no new keystroke --

    [Fact]
    public void HiddenPane_HidesTheRootButKeepsReparsing_AndReAnchorsWhenReactivated()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);

        view.Session.Text = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"100\" Height=\"80\"></Window>";
        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 21);

        MGWindow previewRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        Assert.Equal(Visibility.Visible, previewRoot.Visibility);

        DockPanelNode previewPanel = host.FindPanel(XamlEditorView.PreviewDockableId);
        DockTabGroupNode treeGroup = host.GetAllTabGroups()
            .Single(group => group.Panels.Any(panel => panel.Id == XamlEditorView.TreeDockableId));
        DockOperation.DockAsTab(host.LayoutModel, previewPanel, treeGroup);
        // Merging activates the newly added panel: switch back to "Document" to make "Preview" the inactive tab.
        treeGroup.SetActivePanel(XamlEditorView.TreeDockableId);
        Frame(runtime, desktop, 22);

        // "Preview" is now an inactive tab.
        Assert.Null(view.PreviewPane.Parent);
        Assert.Equal(Visibility.Hidden, previewRoot.Visibility);

        // A re-parse still happens while the pane is hidden.
        int versionWhileHidden = view.PreviewHost.PreviewVersion;
        view.Session.Text = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"120\" Height=\"90\"></Window>";
        Frame(runtime, desktop, 23);
        Frame(runtime, desktop, 40); // past the debounce delay
        Assert.True(view.PreviewHost.PreviewVersion > versionWhileHidden);
        MGWindow rootWhileHidden = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        Assert.Equal(Visibility.Hidden, rootWhileHidden.Visibility);

        // Reactivating the tab re-attaches, re-shows and re-anchors the root without any new keystroke.
        treeGroup.SetActivePanel(XamlEditorView.PreviewDockableId);
        Frame(runtime, desktop, 41);
        Frame(runtime, desktop, 42);

        Assert.NotNull(view.PreviewPane.Parent);
        Assert.Equal(Visibility.Visible, rootWhileHidden.Visibility);
        Rectangle paneBounds = view.PreviewPane.LayoutBounds;
        Assert.Equal(new Point(paneBounds.X, paneBounds.Y), rootWhileHidden.TopLeft);
    }

    /// <summary>Hiding the root while the pane is detached must not turn a root the document declared hidden into a
    /// visible one: the declared visibility is what comes back when the pane is attached again.</summary>
    [Fact]
    public void DeclaredRootVisibility_SurvivesTheHiddenPaneCycle()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, MGDockHost host) = CreateHostedView(1280, 720);

        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"hidden by the document\" Visibility=\"Collapsed\" />";
        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 21);

        MGElement previewRoot = Assert.IsType<MGTextBlock>(view.PreviewHost.PreviewRoot);
        Assert.Equal(Visibility.Collapsed, previewRoot.Visibility);

        DockPanelNode previewPanel = host.FindPanel(XamlEditorView.PreviewDockableId);
        DockTabGroupNode treeGroup = host.GetAllTabGroups()
            .Single(group => group.Panels.Any(panel => panel.Id == XamlEditorView.TreeDockableId));
        DockOperation.DockAsTab(host.LayoutModel, previewPanel, treeGroup);
        treeGroup.SetActivePanel(XamlEditorView.TreeDockableId);
        Frame(runtime, desktop, 22);

        Assert.Null(view.PreviewPane.Parent);
        Assert.Equal(Visibility.Hidden, previewRoot.Visibility);

        treeGroup.SetActivePanel(XamlEditorView.PreviewDockableId);
        Frame(runtime, desktop, 23);
        Frame(runtime, desktop, 24);

        Assert.NotNull(view.PreviewPane.Parent);
        Assert.Equal(Visibility.Collapsed, previewRoot.Visibility);
    }

    // -- 7. IsInteractive: false blocks input to the preview; true lets a click reach a non-Window root's button --

    [Fact]
    public void IsInteractive_GatesInputToThePreviewRoot()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        view.Session.Text = "<Button xmlns=\"" + Ns + "\" Width=\"80\" Height=\"30\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" />";
        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 21);

        MGButton previewButton = Assert.IsType<MGButton>(view.PreviewHost.PreviewRoot);
        int clickCount = 0;
        previewButton.AddCommandHandler((_, _) => clickCount++);

        Point buttonCenter = previewButton.ActualLayoutBounds.Center;
        Assert.False(view.PreviewHost.IsInteractive);

        Frame(runtime, desktop, 22, buttonCenter);
        Frame(runtime, desktop, 23, buttonCenter, MouseButton.Left);
        Frame(runtime, desktop, 24, buttonCenter);
        Assert.Equal(0, clickCount);

        view.PreviewHost.IsInteractive = true;
        Frame(runtime, desktop, 25, buttonCenter);
        Frame(runtime, desktop, 26, buttonCenter, MouseButton.Left);
        Frame(runtime, desktop, 27, buttonCenter);
        Assert.Equal(1, clickCount);
    }

    // -- 8. Preview elements carry the session's SourceName --

    [Fact]
    public void PreviewElements_CarryTheSessionSourceName()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        view.Session.SourceName = "my-document.xaml";
        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"Hi\" />";
        Frame(runtime, desktop, 20);

        Assert.NotNull(view.PreviewHost.PreviewRoot);
        Assert.True(UIToolingService.TryGetXamlSourcePosition(view.PreviewHost.PreviewRoot, out XamlSourcePosition position));
        Assert.Equal("my-document.xaml", position.SourceName);
    }

    // -- Additional: empty text clears the preview without a diagnostic --

    [Fact]
    public void EmptyText_ClearsThePreview_WithNoDiagnostic()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"Hi\" />";
        Frame(runtime, desktop, 20);
        Assert.NotNull(view.PreviewHost.PreviewRoot);

        view.Session.Text = "   ";
        Frame(runtime, desktop, 40);

        Assert.Null(view.PreviewHost.PreviewRoot);
        Assert.Empty(view.PreviewHost.Diagnostics);
    }

    // -- The text pane is the session's text: typing renders, and writing the session writes the pane back --

    /// <summary>What the user actually does: type in the "XAML" pane and wait. The text must reach the session and the
    /// preview must render it, with no code touching <see cref="XamlEditorSession.Text"/> directly.</summary>
    [Fact]
    public void TypingInTheTextPane_ReachesTheSession_AndRendersThePreview()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);
        string markup = "<Window xmlns=\"" + Ns + "\" Left=\"440\" Top=\"20\" Width=\"300\" Height=\"200\"><Button Content=\"Salut\" /></Window>";

        view.TextPane.SetText(markup);

        Assert.Equal(markup, view.Session.Text);
        Assert.Null(view.PreviewHost.PreviewRoot);

        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 40); // past the debounce delay

        MGWindow previewRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        Assert.Empty(view.PreviewHost.Diagnostics);
        Rectangle paneBounds = view.PreviewPane.LayoutBounds;
        Assert.Equal(new Point(paneBounds.X + 440, paneBounds.Y + 20), previewRoot.TopLeft);
    }

    /// <summary>The other direction, which slices X6 (grid edits) and X7 (opening a file) need: writing the session's
    /// text puts it in the pane, and neither side echoes the other into a loop.</summary>
    [Fact]
    public void WritingTheSessionText_WritesTheTextPaneBack_WithoutLooping()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        int sessionChanges = 0;
        view.Session.TextChanged += (_, _) => sessionChanges++;
        int paneChanges = 0;
        view.TextPane.TextChanged += (_, _) => paneChanges++;

        view.Session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"from the session\" />";
        Frame(runtime, desktop, 20);

        Assert.Equal(view.Session.Text, view.TextPane.Text);
        Assert.Equal(1, sessionChanges);
        Assert.Equal(1, paneChanges);

        view.TextPane.SetText("<TextBlock xmlns=\"" + Ns + "\" Text=\"from the pane\" />");
        Frame(runtime, desktop, 21);

        Assert.Equal(view.TextPane.Text, view.Session.Text);
        Assert.Equal(2, sessionChanges);
        Assert.Equal(2, paneChanges);
    }

    /// <summary>A previewed root keeps the size its document declares instead of filling the pane: the pane allocates
    /// what the presenter asks for, and the presenter asks for the root's own size.</summary>
    [Fact]
    public void PreviewedRoots_KeepTheirOwnSize_InsteadOfFillingThePane()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, _, XamlEditorView view, _) = CreateHostedView(1280, 720);

        view.TextPane.SetText("<Window xmlns=\"" + Ns + "\" Left=\"50\" Top=\"20\" Width=\"200\" Height=\"100\"><Button Content=\"Salut\" /></Window>");
        Frame(runtime, desktop, 20);
        Frame(runtime, desktop, 40);
        Frame(runtime, desktop, 41);

        MGWindow windowRoot = Assert.IsType<MGWindow>(view.PreviewHost.PreviewRoot);
        Rectangle paneBounds = view.PreviewPane.LayoutBounds;
        Assert.True(paneBounds.Width > 250 && paneBounds.Height > 120, "the pane must be bigger than the previewed window for this test to mean anything");
        //  The declared size is kept, and the declared Left and Top place the window from the pane's corner.
        Assert.Equal(new Rectangle(paneBounds.X + 50, paneBounds.Y + 20, 200, 100), windowRoot.LayoutBounds);

        // A root that is not a Window is shown at its natural size too, at the same corner.
        view.TextPane.SetText("<Button xmlns=\"" + Ns + "\" Content=\"Plain\" />");
        Frame(runtime, desktop, 60);
        Frame(runtime, desktop, 80);
        Frame(runtime, desktop, 81);

        MGElement plainRoot = Assert.IsType<MGButton>(view.PreviewHost.PreviewRoot);
        Assert.Equal(new Point(paneBounds.X, paneBounds.Y), new Point(plainRoot.LayoutBounds.X, plainRoot.LayoutBounds.Y));
        Assert.True(plainRoot.LayoutBounds.Width > 0 && plainRoot.LayoutBounds.Width < paneBounds.Width);
        Assert.True(plainRoot.LayoutBounds.Height > 0 && plainRoot.LayoutBounds.Height < paneBounds.Height);
    }

    /// <summary>A session whose text is already set before the view exists renders without a first keystroke.</summary>
    [Fact]
    public void SessionTextSetBeforeTheView_ReachesThePaneAndThePreview()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 720));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 1280, 720)
        {
            WindowStyle = WindowStyle.None,
        };

        XamlEditorSession session = new();
        session.Text = "<TextBlock xmlns=\"" + Ns + "\" Text=\"opened before the view\" />";
        XamlEditorView view = new(window, session);

        MGDockHost host = view.CreateDockHost();
        window.SetContent(host);
        desktop.Windows.Add(window);

        Assert.Equal(session.Text, view.TextPane.Text);

        Frame(runtime, desktop, 0);
        Frame(runtime, desktop, 40); // past the debounce delay

        Assert.IsType<MGTextBlock>(view.PreviewHost.PreviewRoot);
    }
}
