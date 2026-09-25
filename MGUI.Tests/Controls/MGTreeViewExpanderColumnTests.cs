using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Controls;

/// <summary>
/// The expander column of an <see cref="MGTreeViewItem"/> is reserved at the same width whether the item has children
/// (draws a triangle) or is a leaf (draws nothing): a leaf's header text must start at the same X as a sibling that has
/// children, instead of shifting left by the width the triangle would have occupied.
/// </summary>
public class MGTreeViewExpanderColumnTests
{
    /// <summary>The column width a root-level item (no indentation) reserves before its header text, computed without
    /// reaching into any private field: at level 0 the indentation column is 0 px wide, so the whole offset between the
    /// item's own left edge and its header content's left edge is the expander column.</summary>
    private static int GetRootLevelExpanderColumnWidth(MGTreeViewItem item)
        => item.HeaderContent.ActualLayoutBounds.X - item.ActualLayoutBounds.X;

    [Fact]
    public void Siblings_With_And_Without_Children_Have_The_Same_Header_X_At_Root_Level()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        MGTreeViewItem branch = new(harness.Window) { Header = "Branch" };
        branch.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        harness.TreeView.AddItem(leaf);
        harness.TreeView.AddItem(branch);
        harness.Show();

        Assert.False(leaf.HasItems);
        Assert.True(branch.HasItems);
        Assert.Equal(leaf.HeaderContent.ActualLayoutBounds.X, branch.HeaderContent.ActualLayoutBounds.X);

        int expected = MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize;
        Assert.Equal(expected, GetRootLevelExpanderColumnWidth(leaf));
        Assert.Equal(expected, GetRootLevelExpanderColumnWidth(branch));
    }

    [Fact]
    public void Siblings_With_And_Without_Children_Have_The_Same_Header_X_Under_A_Shared_Parent()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem parent = new(harness.Window) { Header = "Parent", IsExpanded = true };
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        MGTreeViewItem branch = new(harness.Window) { Header = "Branch" };
        branch.AddItem(new MGTreeViewItem(harness.Window) { Header = "Grandchild" });
        parent.AddItem(leaf);
        parent.AddItem(branch);
        harness.TreeView.AddItem(parent);
        harness.Show();

        Assert.Equal(1, leaf.Level);
        Assert.Equal(1, branch.Level);
        Assert.Equal(leaf.HeaderContent.ActualLayoutBounds.X, branch.HeaderContent.ActualLayoutBounds.X);

        // Not only equal to each other: both headers start one indentation level plus one expander column to the right of
        // the root item's left edge, so a consistent but wrong nested column width (or indent) cannot pass.
        int expectedOffset = leaf.Level * harness.TreeView.IndentSize + harness.TreeView.ExpanderButtonSize;
        Assert.Equal(expectedOffset, leaf.HeaderContent.ActualLayoutBounds.X - parent.ActualLayoutBounds.X);
        Assert.Equal(expectedOffset, branch.HeaderContent.ActualLayoutBounds.X - parent.ActualLayoutBounds.X);
    }

    [Fact]
    public void Theme_Change_Resizes_The_Expander_Column_And_A_Value_Set_On_The_Tree_Wins_Over_The_Theme_Default()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        harness.TreeView.AddItem(leaf);
        harness.Show();

        Assert.Equal(MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize, harness.TreeView.ExpanderButtonSize);

        MGTheme widened = harness.Window.GetTheme().Copy();
        widened.TreeViewExpanderButtonSize = 22;
        harness.SwitchTheme(widened);

        Assert.Equal(22, harness.TreeView.ExpanderButtonSize);
        Assert.Equal(22, GetRootLevelExpanderColumnWidth(leaf));

        // A value set directly on the tree wins over the theme default from then on, even across a later theme change.
        harness.TreeView.ExpanderButtonSize = 12;
        harness.Settle();
        Assert.Equal(12, GetRootLevelExpanderColumnWidth(leaf));

        MGTheme widenedAgain = harness.Window.GetTheme().Copy();
        widenedAgain.TreeViewExpanderButtonSize = 30;
        harness.SwitchTheme(widenedAgain);

        Assert.Equal(12, harness.TreeView.ExpanderButtonSize);
        Assert.Equal(12, GetRootLevelExpanderColumnWidth(leaf));
    }

    [Fact]
    public void A_Zero_Theme_Value_Falls_Back_To_The_Default_Column_Width()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        harness.TreeView.AddItem(leaf);
        harness.Show();

        MGTheme empty = harness.Window.GetTheme().Copy();
        empty.TreeViewExpanderButtonSize = 0;
        harness.SwitchTheme(empty);

        Assert.Equal(MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize, harness.TreeView.ExpanderButtonSize);
        Assert.Equal(MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize, GetRootLevelExpanderColumnWidth(leaf));
    }

    [Fact]
    public void An_Explicit_Zero_On_The_Tree_Also_Falls_Back_To_The_Default_Column_Width()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        harness.TreeView.AddItem(leaf);
        harness.Show();

        harness.TreeView.ExpanderButtonSize = 0;
        harness.Settle();

        // ExpanderButtonSize itself reports the raw value the caller set...
        Assert.Equal(0, harness.TreeView.ExpanderButtonSize);
        // ...but the column an item actually reserves never collapses to 0.
        Assert.Equal(MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize, GetRootLevelExpanderColumnWidth(leaf));
    }

    [Fact]
    public void A_Column_Width_Below_Ten_Pixels_Is_Honored_Exactly()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        harness.TreeView.AddItem(leaf);
        harness.Show();

        harness.TreeView.ExpanderButtonSize = 6;
        harness.Settle();

        Assert.Equal(6, GetRootLevelExpanderColumnWidth(leaf));
    }

    [Fact]
    public void A_Real_Click_In_A_Leafs_Reserved_Column_Selects_It_Instead_Of_Doing_Nothing()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem leaf = new(harness.Window) { Header = "Leaf" };
        harness.TreeView.AddItem(leaf);
        harness.Show();

        Point insideReservedColumn = new(
            leaf.ActualLayoutBounds.X + MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize / 2,
            leaf.ActualLayoutBounds.Center.Y);

        harness.Click(insideReservedColumn);

        Assert.Same(leaf, harness.TreeView.SelectedItem);
    }

    [Fact]
    public void A_Real_Click_On_A_Parents_Triangle_Toggles_It_Without_Selecting_It()
    {
        Harness harness = Harness.Create();
        MGTreeViewItem parent = new(harness.Window) { Header = "Parent" };
        parent.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        harness.TreeView.AddItem(parent);
        harness.Show();

        Assert.False(parent.IsExpanded);

        Point onTriangle = new(
            parent.ActualLayoutBounds.X + MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize / 2,
            parent.ActualLayoutBounds.Center.Y);

        harness.Click(onTriangle);

        Assert.True(parent.IsExpanded);
        Assert.Null(harness.TreeView.SelectedItem);
    }

    [Fact]
    public void Xaml_ExpanderButtonSize_Attribute_Sets_The_Property()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 480));
        MGDesktop desktop = new(runtime);

        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
        Width=""300"" Height=""200"" Padding=""0"">
    <TreeView Name=""Tree"" ExpanderButtonSize=""24"">
        <TreeViewItem Header=""Root"" />
    </TreeView>
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, xaml, false, true);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();

        MGTreeView treeView = window.GetElementByName<MGTreeView>("Tree");
        Assert.Equal(24, treeView.ExpanderButtonSize);
    }

    private sealed class Harness
    {
        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        public MGTreeView TreeView { get; }

        private Harness(GraphTestRuntime runtime, MGDesktop desktop, MGWindow window, MGTreeView treeView)
        {
            Runtime = runtime;
            Desktop = desktop;
            Window = window;
            TreeView = treeView;
        }

        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 480));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 300, 200)
            {
                WindowStyle = WindowStyle.None,
                Padding = new MonoGame.Extended.Thickness(0),
            };
            MGTreeView treeView = new(window);
            window.SetContent(treeView);
            Harness harness = new(runtime, desktop, window, treeView);
            return harness;
        }

        /// <summary>Shows the window and runs enough frames for layout to settle.</summary>
        public void Show()
        {
            if (!Desktop.Windows.Contains(Window))
            {
                Desktop.Windows.Add(Window);
            }

            Frame(0);
            Frame(1);
            Frame(2);
        }

        public void SwitchTheme(MGTheme theme)
        {
            Window.GetResources().DefaultTheme = theme;
            Settle();
        }

        private int _settleFrame = 4;

        /// <summary>Runs two more frames so a property change made directly by the test (not through <see cref="SwitchTheme"/>) is laid out.</summary>
        public void Settle()
        {
            Frame(++_settleFrame);
            Frame(++_settleFrame);
        }

        public void Frame(int frameIndex, Point? position = null, MouseButton? pressedButton = null)
        {
            Point p = position ?? Point.Zero;
            MouseState mouse = new(p.X, p.Y, 0,
                pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
                pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
                pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }

        private int _clickFrame = 10;

        /// <summary>A full press-release cycle at <paramref name="position"/>, close enough in time to register as one click.</summary>
        public void Click(Point position)
        {
            Frame(++_clickFrame, position);
            Frame(++_clickFrame, position, MouseButton.Left);
            Frame(++_clickFrame, position);
        }
    }
}
