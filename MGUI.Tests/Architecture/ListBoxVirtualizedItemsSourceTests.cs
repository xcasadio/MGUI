using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Covers incremental <see cref="MGListBox{TItemType}.ItemsSource"/> changes while UI virtualization is active.
/// Before these were supported, the only way to reflect a new item was to reassign the whole items source, which
/// re-measured the item height and discarded the recycling pool on every single append.</summary>
public class ListBoxVirtualizedItemsSourceTests
{
    /// <summary>A virtualized list box hosted in a laid-out window, with an item template that counts how many
    /// item elements have been generated.</summary>
    private sealed class Harness
    {
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        public MGListBox<string> ListBox { get; }
        public int TemplateInvocations { get; private set; }

        public Harness(int itemHeight = 0, Func<int> itemHeightProvider = null)
        {
            var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 240));
            Desktop = new MGDesktop(runtime);
            Window = new MGWindow(Desktop, 0, 0, 240, 200)
            {
                WindowStyle = WindowStyle.None,
            };

            ListBox = new MGListBox<string>(Window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                VirtualizationMode = ListBoxVirtualizationMode.Always,
            };
            ListBox.ItemTemplate = item =>
            {
                TemplateInvocations++;
                MGTextBlock textBlock = new(Window, item);
                int height = itemHeightProvider?.Invoke() ?? itemHeight;
                if (height > 0)
                {
                    textBlock.PreferredHeight = height;
                }

                return textBlock;
            };

            Window.SetContent(ListBox);
            Desktop.Windows.Add(Window);
        }

        public VirtualizingStackPanel Panel => ListBox.VirtualizingPanel;

        /// <summary>Two ticks: the first applies the pending layout, the second settles any layout refresh it queued.</summary>
        public void Settle()
        {
            Desktop.Update();
            Desktop.Update();
        }

        public ObservableCollection<string> BindSource(int initialCount)
        {
            ObservableCollection<string> source = new(Enumerable.Range(0, initialCount).Select(i => $"item {i}"));
            ListBox.SetItemsSource(source);
            Settle();
            return source;
        }
    }

    /// <summary>Reads the backing delegate of a field-like event, to assert that a code path does not subscribe to it.</summary>
    private static Delegate GetEventSubscribers(object instance, string eventName)
    {
        FieldInfo field = instance.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No backing field found for event '{eventName}'.");
        return (Delegate)field.GetValue(instance);
    }

    [Fact]
    public void Add_WhileVirtualizing_TracksCountAndRealizesItem()
    {
        Harness harness = new();
        ObservableCollection<string> source = harness.BindSource(0);

        //  Used to throw NullReferenceException: the collection-changed handler assumed InternalItems, which is
        //  null while virtualizing.
        source.Add("first");
        source.Add("second");

        harness.Settle();

        Assert.Equal(2, harness.Panel.TotalItemCount);
        Assert.Equal(0, harness.Panel.FirstRealizedIndex);
        Assert.Equal(1, harness.Panel.LastRealizedIndex);
    }

    [Fact]
    public void Add_WhenSourceWasEmpty_MeasuresRealItemHeight()
    {
        Harness harness = new(itemHeight: 40);
        ObservableCollection<string> source = harness.BindSource(0);

        //  There was nothing to measure when the source was bound, so the panel is on its fallback height.
        int emptyHeight = harness.Panel.UniformItemHeight;

        source.Add("first");
        harness.Settle();

        Assert.NotEqual(emptyHeight, harness.Panel.UniformItemHeight);
        Assert.True(harness.Panel.UniformItemHeight >= 40);
    }

    [Fact]
    public void AppendPastRealizedWindow_DoesNotRebuildVisibleItems()
    {
        Harness harness = new();

        //  Start with enough items that the visible range is already saturated.
        ObservableCollection<string> source = harness.BindSource(200);

        int lastRealizedBefore = harness.Panel.LastRealizedIndex;
        Assert.True(lastRealizedBefore > 0);

        int invocationsBefore = harness.TemplateInvocations;

        //  Appending below the realized window must not touch a single realized element: the visible range is
        //  unchanged, so no item is regenerated and the item height is not re-measured.
        for (int i = 0; i < 1000; i++)
        {
            source.Add($"appended {i}");
        }

        harness.Settle();

        Assert.Equal(1200, harness.Panel.TotalItemCount);
        Assert.Equal(lastRealizedBefore, harness.Panel.LastRealizedIndex);
        Assert.Equal(invocationsBefore, harness.TemplateInvocations);
    }

    [Fact]
    public void Insert_BeforeRealizedWindow_RegeneratesAffectedItems()
    {
        Harness harness = new();
        ObservableCollection<string> source = harness.BindSource(200);

        int invocationsBefore = harness.TemplateInvocations;

        //  Every realized element now maps to different data, so the visible window has to be rebuilt.
        source.Insert(0, "inserted");
        harness.Settle();

        Assert.Equal(201, harness.Panel.TotalItemCount);
        Assert.True(harness.TemplateInvocations > invocationsBefore);
    }

    [Fact]
    public void Clear_WhileVirtualizing_ResetsPanelAndSelection()
    {
        Harness harness = new();
        ObservableCollection<string> source = harness.BindSource(50);

        harness.ListBox.SelectItem("item 3", true);
        Assert.NotEmpty(harness.ListBox.SelectedIndices);

        source.Clear();
        harness.Settle();

        Assert.Equal(0, harness.Panel.TotalItemCount);
        Assert.Equal(-1, harness.Panel.FirstRealizedIndex);
        Assert.Empty(harness.ListBox.SelectedIndices);
    }

    [Fact]
    public void RemoveBeforeSelection_RebasesSelectedIndices()
    {
        Harness harness = new();
        ObservableCollection<string> source = harness.BindSource(50);

        harness.ListBox.SelectItem("item 10", true);
        Assert.Equal(new[] { 10 }, harness.ListBox.SelectedIndices.ToArray());

        source.RemoveAt(0);
        harness.Settle();

        //  The selected item is still "item 10", now sitting one row higher.
        Assert.Equal("item 10", source[9]);
        Assert.Equal(new[] { 9 }, harness.ListBox.SelectedIndices.ToArray());
    }

    [Fact]
    public void ItemHeight_IsNotReMeasured_WhenItemsSourceIsReassigned()
    {
        int templateHeight = 30;
        Harness harness = new(itemHeightProvider: () => templateHeight);
        harness.BindSource(200);

        int measuredHeight = harness.Panel.UniformItemHeight;
        Assert.True(measuredHeight >= 30);

        //  The item height depends on the template, the container style and the theme — not on the data.
        //  Reassigning the source must not pay for another probe layout pass.
        templateHeight = 60;
        harness.ListBox.SetItemsSource(new ObservableCollection<string>(Enumerable.Range(0, 200).Select(i => $"other {i}")));
        harness.Settle();

        Assert.Equal(measuredHeight, harness.Panel.UniformItemHeight);

        //  ...but the cache is not a one-way door.
        harness.ListBox.InvalidateItemHeightCache();

        Assert.True(harness.Panel.UniformItemHeight > measuredHeight);
    }

    [Fact]
    public void ReassigningItemsSource_ReusesRecycledWrappers()
    {
        Harness harness = new();
        harness.BindSource(200);

        //  ItemContainerStyle runs once per wrapper construction, so it counts wrapper allocations.
        int wrappersCreated = 0;
        harness.ListBox.ItemContainerStyle = presenter =>
        {
            wrappersCreated++;
            harness.ListBox.ApplyDefaultItemContainerStyle(presenter);
        };
        harness.Settle();
        wrappersCreated = 0;

        //  Each reassignment used to clear the presenter -> wrapper map, which made every pooled element
        //  unresolvable: the generator dropped it and allocated a fresh wrapper for every visible row.
        for (int i = 0; i < 20; i++)
        {
            harness.ListBox.SetItemsSource(new ObservableCollection<string>(Enumerable.Range(0, 200).Select(n => $"pass {i} item {n}")));
            harness.Settle();
        }

        Assert.Equal(0, wrappersCreated);
    }

    [Fact]
    public void ItemWrappers_DoNotSubscribeToOwnerEvents()
    {
        Harness harness = new();
        harness.BindSource(200);

        //  Wrappers used to hook ItemTemplateChanged / ItemContainerStyleChanged with lambdas that were never
        //  removed, rooting every wrapper ever created in those invocation lists.
        Assert.Null(GetEventSubscribers(harness.ListBox, nameof(MGListBox<string>.ItemTemplateChanged)));
        Assert.Null(GetEventSubscribers(harness.ListBox, nameof(MGListBox<string>.ItemContainerStyleChanged)));
    }

    [Fact]
    public void ChangingItemTemplate_StillRefreshesExistingItems()
    {
        Harness harness = new();
        harness.BindSource(200);

        int newTemplateInvocations = 0;
        harness.ListBox.ItemTemplate = item =>
        {
            newTemplateInvocations++;
            return new MGTextBlock(harness.Window, item);
        };

        //  Applied immediately to the wrappers the list box owns, without any per-item subscription.
        Assert.True(newTemplateInvocations > 0);

        harness.Settle();
        Assert.Equal(200, harness.Panel.TotalItemCount);
    }

    [Fact]
    public void RemoveSelectedItem_DropsItFromSelection()
    {
        Harness harness = new();
        ObservableCollection<string> source = harness.BindSource(50);

        harness.ListBox.SelectItem("item 10", true);
        Assert.Equal(new[] { 10 }, harness.ListBox.SelectedIndices.ToArray());

        source.RemoveAt(10);
        harness.Settle();

        Assert.Empty(harness.ListBox.SelectedIndices);
    }
}
