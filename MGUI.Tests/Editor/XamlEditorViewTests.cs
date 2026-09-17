using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Editor;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Editor;

public class XamlEditorViewTests
{
    private static (MGDesktop Desktop, MGWindow Window, XamlEditorView View) CreateView(int width, int height)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, width, height));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, width, height)
        {
            WindowStyle = WindowStyle.None,
        };

        XamlEditorSession session = new();
        XamlEditorView view = new(window, session);
        window.SetContent(view.Root);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        return (desktop, window, view);
    }

    [Fact]
    public void NamedPanes_AreResolvableFromTheWindow_AsTheSameInstances()
    {
        (_, MGWindow window, XamlEditorView view) = CreateView(1280, 720);

        Assert.Same(view.TextPane, window.GetElementByName<MGRichTextBox>(XamlEditorView.TextPaneName));
        Assert.Same(view.PreviewPane, window.GetElementByName<MGOverlayPanel>(XamlEditorView.PreviewPaneName));
        Assert.Same(view.TreePane, window.GetElementByName<MGTreeView>(XamlEditorView.TreePaneName));
        Assert.Same(view.PropertyPane, window.GetElementByName<MGPropertyGrid>(XamlEditorView.PropertyPaneName));
    }

    [Fact]
    public void Layout_OrdersThePanesAndSizesThemNonEmpty_InA1280x720Window()
    {
        (_, _, XamlEditorView view) = CreateView(1280, 720);

        Rectangle textBounds = view.TextPane.LayoutBounds;
        Rectangle previewBounds = view.PreviewPane.LayoutBounds;
        Rectangle treeBounds = view.TreePane.LayoutBounds;
        Rectangle propertyBounds = view.PropertyPane.LayoutBounds;

        Assert.True(textBounds.Width > 0 && textBounds.Height > 0);
        Assert.True(previewBounds.Width > 0 && previewBounds.Height > 0);
        Assert.True(treeBounds.Width > 0 && treeBounds.Height > 0);
        Assert.True(propertyBounds.Width > 0 && propertyBounds.Height > 0);

        Assert.True(textBounds.Right <= previewBounds.Left);
        Assert.True(previewBounds.Right <= treeBounds.Left);

        Assert.True(treeBounds.Bottom <= propertyBounds.Top);
        Assert.Equal(treeBounds.Left, propertyBounds.Left);
        Assert.Equal(treeBounds.Width, propertyBounds.Width);

        Assert.Contains(view.PreviewPresenter, view.PreviewPane.TraverseVisualTree(false, false, false, false));

        int splitterCount = view.Root.TraverseVisualTree(false, false, false, false)
            .Count(element => element is MGGridSplitter);
        Assert.Equal(2, splitterCount);
    }

    [Fact]
    public void Constructor_ThrowsOnNullArguments()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 320, 240));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 320, 240)
        {
            WindowStyle = WindowStyle.None,
        };
        XamlEditorSession session = new();

        Assert.Throws<ArgumentNullException>(() => new XamlEditorView(null, session));
        Assert.Throws<ArgumentNullException>(() => new XamlEditorView(window, null));
    }
}
