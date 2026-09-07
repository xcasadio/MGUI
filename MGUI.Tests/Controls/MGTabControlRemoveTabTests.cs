using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using MGUI.Tests.Graph;
using System.Collections.Generic;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Controls;

/// <summary>
/// Covers what <see cref="MGTabControl.RemoveTab(MGTabItem)"/> must leave behind.<para/>
/// Parent lifecycle: a tab is parented to its <see cref="MGTabControl"/> once, in its constructor, so removal must reset that parent like every
/// other container does (<see cref="MGMultiContentHost"/>, <see cref="MGSingleContentHost"/>). Otherwise the removed tab keeps resolving resources
/// through the tab control's scope, and the <see cref="UIDynamicResourceSubscriptions"/> container bound to it, which only follows
/// <see cref="MGElement.OnParentChanged"/>, never detaches.<para/>
/// Selection lifecycle (ADR-0003): a removed tab never stays <see cref="MGTabControl.SelectedTab"/>. The tab to its left takes over; when no other
/// tab can (last tab removed, or the switch to the neighbour cancelled by <see cref="MGTabControl.SelectedTabChanging"/>), the selection and the
/// displayed content are cleared and <see cref="MGTabControl.SelectedTabChanged"/> reports a null new value.
/// </summary>
public class MGTabControlRemoveTabTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemoveTab_ResetsRemovedTabParent_AndKeepsRemainingTabParented(bool removeSelectedTab)
    {
        TabControlHarness harness = TabControlHarness.Create();
        MGTabItem first = harness.TabControl.AddTab("First", new MGTextBlock(harness.Window, "first"));
        MGTabItem second = harness.TabControl.AddTab("Second", new MGTextBlock(harness.Window, "second"));
        if (!removeSelectedTab)
        {
            Assert.True(harness.TabControl.TrySelectTab(second));
        }

        Assert.Same(harness.TabControl, first.Parent);
        Assert.Same(harness.TabControl, second.Parent);
        Assert.Equal(removeSelectedTab, first.IsTabSelected);

        List<MGElement> removedContent = new();
        harness.TabControl.OnContentRemoved += (_, e) => removedContent.Add(e);

        harness.TabControl.RemoveTab(first);

        Assert.Null(first.Parent);
        Assert.Same(first, Assert.Single(removedContent));
        Assert.DoesNotContain(first, harness.TabControl.Tabs);
        Assert.Same(harness.TabControl, second.Parent);
        Assert.Same(second, harness.TabControl.SelectedTab);
        Assert.Same(second, harness.TabControl.Content);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RemoveTab_SelectedTab_SelectsTheTabToItsLeft(int removedIndex)
    {
        TabControlHarness harness = TabControlHarness.Create();
        MGTabItem[] tabs =
        {
            harness.TabControl.AddTab("First", new MGTextBlock(harness.Window, "first")),
            harness.TabControl.AddTab("Second", new MGTextBlock(harness.Window, "second")),
            harness.TabControl.AddTab("Third", new MGTextBlock(harness.Window, "third")),
        };
        MGTabItem removed = tabs[removedIndex];
        MGTabItem leftNeighbour = tabs[removedIndex - 1];
        Assert.True(harness.TabControl.TrySelectTab(removed));

        harness.TabControl.RemoveTab(removed);

        Assert.Null(removed.Parent);
        Assert.Same(leftNeighbour, harness.TabControl.SelectedTab);
        Assert.Equal(removedIndex - 1, harness.TabControl.SelectedTabIndex);
        Assert.Same(leftNeighbour, harness.TabControl.Content);
        Assert.Equal(2, harness.TabControl.Tabs.Count);
    }

    [Fact]
    public void RemoveTab_DetachesDynamicResourceSubscription_FromTabControlScope()
    {
        TabControlHarness harness = TabControlHarness.Create();
        MGResources tabControlScope = harness.TabControl.EnsureResourceScope(UIResourceScope.Subtree);
        tabControlScope.AddStaticResource("Accent", "one");

        MGTextBlock content = new(harness.Window, string.Empty);
        MGTabItem tab = harness.TabControl.AddTab("Tab", content);
        _ = harness.TabControl.AddTab("Other", new MGTextBlock(harness.Window, string.Empty));

        Assert.Same(tabControlScope, tab.GetResources());
        int scopeSubscribersBeforeRegistration = tabControlScope.StaticResourceLookupSubscriberCount;
        MGResources windowScope = harness.Window.GetResources();
        int windowSubscribersBeforeRegistration = windowScope.StaticResourceLookupSubscriberCount;

        // The reference is hosted by the tab (the element whose parent changes on removal) and targets the tab's content.
        UIResourceReferenceConfig config = new(nameof(MGTextBlock.Text), "Accent", true);
        Assert.True(UIResourceReferenceApplicator.Apply(tab, content, config, tab.GetResources()));
        Assert.Equal("one", content.Text);
        Assert.Equal(scopeSubscribersBeforeRegistration + 1, tabControlScope.StaticResourceLookupSubscriberCount);

        harness.TabControl.RemoveTab(tab);

        Assert.Null(tab.Parent);
        Assert.Equal(scopeSubscribersBeforeRegistration, tabControlScope.StaticResourceLookupSubscriberCount);
        // A removed tab must not fall back onto its window's scope either: that would keep a strong handler there for as long as the window lives.
        Assert.Equal(windowSubscribersBeforeRegistration, windowScope.StaticResourceLookupSubscriberCount);

        tabControlScope.SetStaticResource("Accent", "two");

        Assert.Equal("one", content.Text);
    }

    [Fact]
    public void RemoveTab_LastTab_ClearsSelectionAndContent()
    {
        TabControlHarness harness = TabControlHarness.Create();
        MGTabItem only = harness.TabControl.AddTab("Only", new MGTextBlock(harness.Window, "only"));
        Assert.Same(only, harness.TabControl.SelectedTab);
        Assert.Same(only, harness.TabControl.Content);

        int changingCount = 0;
        harness.TabControl.SelectedTabChanging += (_, _) => changingCount++;
        List<(MGTabItem Previous, MGTabItem New)> changes = new();
        harness.TabControl.SelectedTabChanged += (_, e) => changes.Add((e.PreviousValue, e.NewValue));
        List<string> tabControlNotifications = new();
        harness.TabControl.PropertyChanged += (_, e) => tabControlNotifications.Add(e.PropertyName);
        List<string> tabNotifications = new();
        only.PropertyChanged += (_, e) => tabNotifications.Add(e.PropertyName);

        harness.TabControl.RemoveTab(only);

        Assert.Null(only.Parent);
        Assert.False(only.IsTabSelected);
        Assert.Null(harness.TabControl.SelectedTab);
        Assert.Equal(-1, harness.TabControl.SelectedTabIndex);
        Assert.Null(harness.TabControl.Content);
        Assert.Empty(harness.TabControl.Tabs);
        Assert.Empty(harness.TabControl.GetVisualTreeChildren(true, true));
        // The forced deselection has nothing left to cancel, so it bypasses SelectedTabChanging and reports itself once through SelectedTabChanged.
        Assert.Equal(0, changingCount);
        (MGTabItem Previous, MGTabItem New) change = Assert.Single(changes);
        Assert.Same(only, change.Previous);
        Assert.Null(change.New);
        // Bindings observe the cleared selection the same way they observe a normal selection change.
        Assert.Contains(nameof(MGTabControl.SelectedTab), tabControlNotifications);
        Assert.Contains(nameof(MGTabControl.SelectedTabIndex), tabControlNotifications);
        Assert.Contains(nameof(MGTabItem.IsTabSelected), tabNotifications);

        // The emptied control must survive a real frame: nothing is left to update, and the removed tab is not reached.
        harness.Desktop.Update();
        harness.Desktop.Update();

        Assert.Null(harness.TabControl.SelectedTab);
        Assert.Null(harness.TabControl.Content);
        Assert.Null(only.Parent);
    }

    [Fact]
    public void AddTab_AfterTheLastTabWasRemoved_SelectsTheNewTab()
    {
        TabControlHarness harness = TabControlHarness.Create();
        MGTabItem only = harness.TabControl.AddTab("Only", new MGTextBlock(harness.Window, "only"));
        harness.TabControl.RemoveTab(only);

        MGTabItem next = harness.TabControl.AddTab("Next", new MGTextBlock(harness.Window, "next"));

        Assert.Same(harness.TabControl, next.Parent);
        Assert.Same(next, harness.TabControl.SelectedTab);
        Assert.Equal(0, harness.TabControl.SelectedTabIndex);
        Assert.Same(next, harness.TabControl.Content);
        Assert.False(only.IsTabSelected);
        Assert.Same(next, Assert.Single(harness.TabControl.GetVisualTreeChildren(true, true)));
    }

    [Fact]
    public void RemoveTab_SelectedTab_WhenTheReplacementSelectionIsCancelled_ClearsSelectionAndKeepsTheOtherTab()
    {
        TabControlHarness harness = TabControlHarness.Create();
        MGTabItem first = harness.TabControl.AddTab("First", new MGTextBlock(harness.Window, "first"));
        MGTabItem second = harness.TabControl.AddTab("Second", new MGTextBlock(harness.Window, "second"));
        Assert.Same(first, harness.TabControl.SelectedTab);

        List<MGTabItem> offeredTabs = new();
        harness.TabControl.SelectedTabChanging += (_, e) =>
        {
            offeredTabs.Add(e.Data);
            e.Cancel = true;
        };
        List<(MGTabItem Previous, MGTabItem New)> changes = new();
        harness.TabControl.SelectedTabChanged += (_, e) => changes.Add((e.PreviousValue, e.NewValue));

        harness.TabControl.RemoveTab(first);

        // The neighbour was offered through SelectedTabChanging and refused; the removed tab still cannot stay selected.
        Assert.Same(second, Assert.Single(offeredTabs));
        Assert.Null(first.Parent);
        Assert.Null(harness.TabControl.SelectedTab);
        Assert.Null(harness.TabControl.Content);
        Assert.Same(harness.TabControl, second.Parent);
        Assert.Same(second, Assert.Single(harness.TabControl.Tabs));
        Assert.False(second.IsTabSelected);
        (MGTabItem Previous, MGTabItem New) change = Assert.Single(changes);
        Assert.Same(first, change.Previous);
        Assert.Null(change.New);
    }

    private sealed class TabControlHarness
    {
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        public MGTabControl TabControl { get; }

        private TabControlHarness(MGDesktop desktop, MGWindow window, MGTabControl tabControl)
        {
            Desktop = desktop;
            Window = window;
            TabControl = tabControl;
        }

        public static TabControlHarness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 240, 200);
            desktop.Windows.Add(window);

            MGTabControl tabControl = new(window);
            window.SetContent(tabControl);
            return new(desktop, window, tabControl);
        }
    }
}
