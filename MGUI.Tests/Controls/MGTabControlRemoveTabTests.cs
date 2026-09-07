using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using MGUI.Tests.Graph;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Controls;

/// <summary>
/// Covers the parent lifecycle of a removed <see cref="MGTabItem"/>. A tab is parented to its <see cref="MGTabControl"/> once, in its
/// constructor, so <see cref="MGTabControl.RemoveTab(MGTabItem)"/> must reset that parent on removal like every other container does
/// (<see cref="MGMultiContentHost"/>, <see cref="MGSingleContentHost"/>). Otherwise the removed tab keeps resolving resources through the
/// tab control's scope, and the <see cref="UIDynamicResourceSubscriptions"/> container bound to it, which only follows
/// <see cref="MGElement.OnParentChanged"/>, never detaches.
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

        harness.TabControl.RemoveTab(first);

        Assert.Null(first.Parent);
        Assert.DoesNotContain(first, harness.TabControl.Tabs);
        Assert.Same(harness.TabControl, second.Parent);
        Assert.Same(second, harness.TabControl.SelectedTab);
        Assert.Same(second, harness.TabControl.Content);
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

    private sealed class TabControlHarness
    {
        public MGWindow Window { get; }
        public MGTabControl TabControl { get; }

        private TabControlHarness(MGWindow window, MGTabControl tabControl)
        {
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
            return new(window, tabControl);
        }
    }
}
