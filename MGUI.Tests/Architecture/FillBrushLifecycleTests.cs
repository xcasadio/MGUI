using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Covers the paint lifecycle contract: <see cref="IFillBrush.Update(UpdateBaseArgs)"/> exists, composite paints forward it,
/// and <see cref="MGElement.Update(ElementUpdateArgs)"/> ticks every fill brush slot that an element draws itself, exactly once per frame.</summary>
public class FillBrushLifecycleTests
{
    private static readonly UpdateBaseArgs OneFrame = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default);

    #region Interface contract

    [Fact]
    public void IFillBrush_ExposesDefaultUpdateHook()
    {
        MethodInfo method = typeof(IFillBrush).GetMethod(nameof(IFillBrush.Update), new[] { typeof(UpdateBaseArgs) });

        Assert.NotNull(method);
        Assert.False(method.IsAbstract);
    }

    [Fact]
    public void StatelessFillBrushes_DoNotOverrideUpdate()
    {
        Type[] statelessBrushes =
        {
            typeof(MGSolidFillBrush), typeof(MGGradientFillBrush), typeof(MGDiagonalGradientFillBrush), typeof(MGTextureFillBrush),
            typeof(MGNineSliceFillBrush), typeof(MGProgressBarGradientBrush), typeof(MGHighlightFillBrush)
        };

        foreach (Type brushType in statelessBrushes)
        {
            MethodInfo declared = brushType.GetMethod(nameof(IFillBrush.Update), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(UpdateBaseArgs) }, null);
            Assert.True(declared == null, $"{brushType.Name} is stateless and must keep the default no-op Update.");
        }
    }

    #endregion

    #region Composite fill brushes

    [Fact]
    public void BorderedFillBrush_WithHighlightBorderBrush_ConstructsWithoutThrowing()
    {
        MGHighlightBorderBrush highlight = new(MGUniformBorderBrush.Black, Color.Yellow, HighlightAnimation.Pulse);

        MGBorderedFillBrush brush = new(new Thickness(2), highlight, SolidFillBrushes.White, false);

        Assert.Same(highlight, brush.BorderBrush);
    }

    [Fact]
    public void BorderedFillBrush_Update_ForwardsToBorderAndFillBrushes()
    {
        RecordingBorderBrush border = new();
        RecordingFillBrush fill = new();
        MGBorderedFillBrush brush = new(new Thickness(2), border, fill, false);

        brush.Update(OneFrame);

        Assert.Equal(1, border.UpdateCount);
        Assert.Equal(1, fill.UpdateCount);
    }

    [Fact]
    public void BorderedFillBrush_Update_AdvancesHighlightAnimation()
    {
        MGHighlightBorderBrush highlight = new(MGUniformBorderBrush.Black, Color.Yellow, HighlightAnimation.Pulse);
        MGBorderedFillBrush brush = new(new Thickness(2), highlight, SolidFillBrushes.White, false);
        TimeSpan pulseCycle = highlight.PulseFadeDuration + highlight.PulseDelay;
        TimeSpan quarterCycle = TimeSpan.FromTicks(pulseCycle.Ticks / 4);

        Assert.Equal(0.0, highlight.AnimationProgress);
        brush.Update(new UpdateBaseArgs(quarterCycle, quarterCycle, default, default));

        Assert.Equal(0.25, highlight.AnimationProgress, 6);
    }

    [Fact]
    public void CompositedFillBrush_Update_ForwardsToEveryChild()
    {
        RecordingFillBrush first = new();
        RecordingFillBrush second = new();
        MGCompositedFillBrush brush = new(first, second);

        brush.Update(OneFrame);

        Assert.Equal(1, first.UpdateCount);
        Assert.Equal(1, second.UpdateCount);
    }

    [Fact]
    public void PaddedFillBrush_Update_ForwardsToChild()
    {
        RecordingFillBrush child = new();
        MGPaddedFillBrush brush = new(child, new Thickness(3));

        brush.Update(OneFrame);

        Assert.Equal(1, child.UpdateCount);
    }

    [Fact]
    public void VisualStateFillBrush_Update_TicksSharedInstanceOnce()
    {
        RecordingFillBrush shared = new();
        VisualStateFillBrush brush = new(shared);

        brush.Update(OneFrame);

        Assert.Same(shared, brush.NormalValue);
        Assert.Same(shared, brush.SelectedValue);
        Assert.Same(shared, brush.FocusedValue);
        Assert.Equal(1, shared.UpdateCount);
    }

    [Fact]
    public void VisualStateFillBrush_Update_TicksDistinctStateBrushes()
    {
        RecordingFillBrush normal = new();
        RecordingFillBrush selected = new();
        RecordingFillBrush focused = new();
        RecordingFillBrush disabled = new();
        VisualStateFillBrush brush = new(normal, selected, focused, disabled, null, PressedModifierType.Darken, 0.06f);

        brush.Update(OneFrame);

        Assert.Equal(1, normal.UpdateCount);
        Assert.Equal(1, selected.UpdateCount);
        Assert.Equal(1, focused.UpdateCount);
        Assert.Equal(1, disabled.UpdateCount);
    }

    #endregion

    #region Border brushes wrapping fill brushes

    [Fact]
    public void UniformBorderBrush_Update_ForwardsToFillBrush()
    {
        RecordingFillBrush fill = new();
        MGUniformBorderBrush brush = new(fill);

        ((IBorderBrush)brush).Update(OneFrame);

        Assert.Equal(1, fill.UpdateCount);
    }

    [Fact]
    public void DockedBorderBrush_Update_TicksEachDistinctSideOnce()
    {
        RecordingFillBrush left = new();
        RecordingFillBrush top = new();
        RecordingFillBrush right = new();
        RecordingFillBrush bottom = new();
        MGDockedBorderBrush distinct = new(left, top, right, bottom);

        ((IBorderBrush)distinct).Update(OneFrame);

        Assert.Equal(1, left.UpdateCount);
        Assert.Equal(1, top.UpdateCount);
        Assert.Equal(1, right.UpdateCount);
        Assert.Equal(1, bottom.UpdateCount);

        RecordingFillBrush shared = new();
        MGDockedBorderBrush sharing = new(shared, shared, shared, shared);

        ((IBorderBrush)sharing).Update(OneFrame);

        Assert.Equal(1, shared.UpdateCount);
    }

    #endregion

    #region Per-frame paint dedup (PaintLifecycle / MGDesktop.Update)

    [Fact]
    public void Desktop_Update_TicksFillBrushSharedByReferenceAcrossTwoElementsOncePerFrame()
    {
        Harness harness = Harness.Create();
        RecordingFillBrush shared = new();
        MGBorder first = new(harness.Window) { BackgroundBrush = new VisualStateFillBrush(shared) };
        MGBorder second = new(harness.Window) { BackgroundBrush = new VisualStateFillBrush(shared) };
        harness.ShowAll(first, second);

        shared.Reset();
        harness.Desktop.Update();
        Assert.Equal(1, shared.UpdateCount);

        harness.Desktop.Update();
        Assert.Equal(2, shared.UpdateCount);
    }

    [Fact]
    public void Desktop_Update_TicksFillBrushReferencedDirectlyAndThroughCompositeOnce()
    {
        Harness harness = Harness.Create();
        RecordingFillBrush shared = new();
        RecordingFillBrush other = new();
        MGBorder direct = new(harness.Window) { OverlayBrush = shared };
        MGBorder composited = new(harness.Window) { OverlayBrush = new MGCompositedFillBrush(shared, other) };
        harness.ShowAll(direct, composited);

        shared.Reset();
        other.Reset();
        harness.Desktop.Update();

        Assert.Equal(1, shared.UpdateCount);
        Assert.Equal(1, other.UpdateCount);
    }

    [Fact]
    public void Desktop_Update_TicksBorderBrushSharedAcrossTwoBordersOncePerFrame()
    {
        Harness harness = Harness.Create();
        RecordingBorderBrush shared = new();
        MGBorder first = new(harness.Window, new Thickness(1), shared);
        MGBorder second = new(harness.Window, new Thickness(1), shared);
        harness.ShowAll(first, second);

        shared.Reset();
        harness.Desktop.Update();
        Assert.Equal(1, shared.UpdateCount);

        harness.Desktop.Update();
        Assert.Equal(2, shared.UpdateCount);
    }

    /// <summary>Reaches the same <see cref="RecordingBorderBrush"/> through <see cref="MGCompositedBorderBrush"/>,
    /// <see cref="MGBandedBorderBrush"/> and as an <see cref="MGHighlightBorderBrush.Underlay"/> on one element, while a second
    /// element references it directly as its own <see cref="MGBorder.BorderBrush"/>: covers the shared paint dedup scenario (Docs/drawing-architecture.md).</summary>
    [Fact]
    public void Desktop_Update_TicksBorderBrushReachedThroughCompositesUnderlayAndDirectReferenceOnce()
    {
        Harness harness = Harness.Create();
        RecordingBorderBrush shared = new();
        MGHighlightBorderBrush highlight = new(shared, Color.Yellow, HighlightAnimation.Pulse);
        MGBandedBorderBrush banded = new(new MGBorderBand(highlight, 1.0));
        MGCompositedBorderBrush composited = new(banded);

        MGBorder nested = new(harness.Window, new Thickness(1), composited);
        MGBorder direct = new(harness.Window, new Thickness(1), shared);
        harness.ShowAll(nested, direct);

        shared.Reset();
        harness.Desktop.Update();
        Assert.Equal(1, shared.UpdateCount);

        harness.Desktop.Update();
        Assert.Equal(2, shared.UpdateCount);
    }

    [Fact]
    public void Update_WithNullPaintRegistry_TicksOnEveryCall()
    {
        RecordingFillBrush brush = new();
        UpdateBaseArgs nullRegistryArgs = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default);
        Assert.Null(nullRegistryArgs.PaintRegistry);

        brush.Update(nullRegistryArgs);
        brush.Update(nullRegistryArgs);

        Assert.Equal(2, brush.UpdateCount);
    }

    [Fact]
    public void GetTranslated_RoundTripsPaintRegistry()
    {
        FakeRegistry registry = new();
        UpdateBaseArgs original = new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default)
            with
        { PaintRegistry = registry };

        UpdateBaseArgs translated = original.GetTranslated(5, 7);

        Assert.Same(registry, translated.PaintRegistry);
        Assert.Equal(original.TotalElapsed, translated.TotalElapsed);
        Assert.Equal(original.FrameElapsed, translated.FrameElapsed);
    }

    #endregion

    #region MGElement wiring

    [Fact]
    public void Element_Update_TicksBackgroundOverlayAndBorderBrushes()
    {
        Harness harness = Harness.Create();
        RecordingFillBrush background = new();
        RecordingFillBrush overlay = new();
        RecordingBorderBrush border = new();
        MGBorder element = new(harness.Window)
        {
            BackgroundBrush = new VisualStateFillBrush(background),
            OverlayBrush = overlay,
            BorderBrush = border,
        };
        harness.Show(element);

        background.Reset();
        overlay.Reset();
        border.Reset();
        harness.Desktop.Update();

        Assert.Equal(1, background.UpdateCount);
        Assert.Equal(1, overlay.UpdateCount);
        Assert.Equal(1, border.UpdateCount);
    }

    [Fact]
    public void Border_WithHighlightInsideUniformBorderBrush_AnimatesEachFrame()
    {
        Harness harness = Harness.Create();
        MGHighlightBorderBrush highlight = new(MGUniformBorderBrush.Black, Color.Yellow, HighlightAnimation.Pulse);
        MGBorderedFillBrush bordered = new(new Thickness(1), highlight, SolidFillBrushes.White, false);
        //  The IFillBrush overload wraps the fill in an MGUniformBorderBrush (MGBorder.cs), which must forward Update.
        MGBorder element = new(harness.Window, new Thickness(2), (IFillBrush)bordered);
        harness.Show(element);

        double afterWarmUp = highlight.AnimationProgress;
        harness.Desktop.Update();

        Assert.True(afterWarmUp > 0.0, "the highlight must already have advanced during the warm-up frames");
        Assert.True(highlight.AnimationProgress > afterWarmUp, "each frame must advance the highlight animation");
    }

    [Fact]
    public void Element_GetFillBrushes_DefaultYieldsOverlayBrush()
    {
        Harness harness = Harness.Create();
        RecordingFillBrush overlay = new();
        HookProbeBorder probe = new(harness.Window) { OverlayBrush = overlay };

        Assert.Equal(new IFillBrush[] { overlay }, probe.FillBrushes.ToArray());
    }

    [Fact]
    public void Element_GetVisualStateFillBrushes_DefaultYieldsBackgroundBrush()
    {
        Harness harness = Harness.Create();
        VisualStateFillBrush background = new(new RecordingFillBrush());
        HookProbeBorder probe = new(harness.Window) { BackgroundBrush = background };

        Assert.Equal(new[] { background }, probe.VisualStateFillBrushes.ToArray());
    }

    #endregion

    #region Controls that draw their own fill brush slots

    /// <summary>One row per control that overrides the hooks. Each row injects a distinct recording brush into every slot and returns them.</summary>
    public static IEnumerable<object[]> DirectSlotControls()
    {
        foreach (DirectSlotCase testCase in DirectSlotRegistry)
        {
            yield return new object[] { testCase };
        }
    }

    [Theory]
    [MemberData(nameof(DirectSlotControls))]
    public void Control_Update_TicksEveryDirectFillBrushSlotOnce(DirectSlotCase testCase)
    {
        Harness harness = Harness.Create();
        MGElement element = testCase.Create(harness.Window);
        harness.Show(element);

        IReadOnlyList<RecordingFillBrush> recorders = testCase.Inject(element);
        harness.Desktop.Update();

        Assert.Equal(testCase.SlotNames.Count, recorders.Count);
        for (int i = 0; i < recorders.Count; i++)
        {
            Assert.True(recorders[i].UpdateCount == 1, $"{testCase.Name}.{testCase.SlotNames[i]} was ticked {recorders[i].UpdateCount} time(s) instead of exactly once.");
        }
    }

    /// <summary>Guard: every public IFillBrush / VisualStateFillBrush slot declared on an MGElement subclass must either be covered by
    /// <see cref="DirectSlotRegistry"/> (ticked by the hooks) or be a documented exclusion. A new slot fails this test until it is classified.</summary>
    [Fact]
    public void FillBrushSlots_AreTickedOrDocumentedExclusions()
    {
        HashSet<string> ticked = DirectSlotRegistry
            .SelectMany(x => x.SlotNames.Select(slot => $"{x.ElementType.Name}.{slot}"))
            .ToHashSet();
        HashSet<string> excluded = DocumentedExclusions.Keys.ToHashSet();

        List<string> unclassified = new();
        List<string> discovered = new();
        foreach (Type elementType in typeof(MGElement).Assembly.GetTypes().Where(x => x != typeof(MGElement) && x.IsSubclassOf(typeof(MGElement))))
        {
            foreach (PropertyInfo property in elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!IsFillBrushSlot(property))
                {
                    continue;
                }

                string key = $"{TypeName(elementType)}.{property.Name}";
                discovered.Add(key);
                if (!ticked.Contains(key) && !excluded.Contains(key))
                {
                    unclassified.Add(key);
                }
            }
        }

        Assert.True(unclassified.Count == 0, "Unclassified fill brush slots (add them to DirectSlotRegistry and the control's GetFillBrushes/GetVisualStateFillBrushes override, or to DocumentedExclusions with a reason): " + string.Join(", ", unclassified));
        Assert.All(ticked, key => Assert.Contains(key, discovered));
        Assert.All(excluded, key => Assert.Contains(key, discovered));
    }

    /// <summary>Type name without the generic arity suffix, so that MGListBox&lt;T&gt; is keyed as "MGListBox".</summary>
    private static string TypeName(Type type)
    {
        int arity = type.Name.IndexOf('`');
        return arity < 0 ? type.Name : type.Name[..arity];
    }

    private static bool IsFillBrushSlot(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0)
        {
            return false;
        }

        Type type = property.PropertyType;
        bool isBrushType = type == typeof(IFillBrush) || type == typeof(VisualStateFillBrush);
        bool isBrushCollection = type != typeof(string) && typeof(IEnumerable<IFillBrush>).IsAssignableFrom(type);
        if (!isBrushType && !isBrushCollection)
        {
            return false;
        }

        //  Get-only wrappers (MGSlider.Actual*, MGElement.BackgroundUnderlay/Overlay) resolve to another slot and are never ticked themselves.
        return isBrushCollection || (property.SetMethod?.IsPublic ?? false);
    }

    private static readonly IReadOnlyList<DirectSlotCase> DirectSlotRegistry = new DirectSlotCase[]
    {
        new(typeof(MGSlider), new[] { "NumberLineFillBrush", "TickFillBrush", "ThumbFillBrush", "Foreground", "FocusBrush" },
            window => new MGSlider(window, 0, 100, 50),
            element =>
            {
                MGSlider slider = (MGSlider)element;
                RecordingFillBrush[] recorders = Recorders(5);
                slider.NumberLineFillBrush = recorders[0];
                slider.TickFillBrush = recorders[1];
                slider.ThumbFillBrush = recorders[2];
                slider.Foreground = recorders[3];
                slider.FocusBrush = new VisualStateFillBrush(recorders[4]);
                return recorders;
            }),
        new(typeof(MGRectangle), new[] { "Fill" },
            window => new MGRectangle(window, 40, 20, Color.White, 1, Color.Red),
            element =>
            {
                RecordingFillBrush[] recorders = Recorders(1);
                ((MGRectangle)element).Fill = recorders[0];
                return recorders;
            }),
        new(typeof(MGShapeElementBase), new[] { "FillBrush" },
            window => new MGEllipse(window, 40, 20),
            element =>
            {
                RecordingFillBrush[] recorders = Recorders(1);
                ((MGEllipse)element).FillBrush = recorders[0];
                return recorders;
            }),
        new(typeof(MGScrollViewer), new[] { "ScrollBarOuterBrush", "ScrollBarInnerBrush" },
            window => new MGScrollViewer(window),
            element =>
            {
                MGScrollViewer scrollViewer = (MGScrollViewer)element;
                RecordingFillBrush[] recorders = Recorders(2);
                scrollViewer.ScrollBarOuterBrush = new VisualStateFillBrush(recorders[0]);
                scrollViewer.ScrollBarInnerBrush = new VisualStateFillBrush(recorders[1]);
                return recorders;
            }),
        new(typeof(MGProgressButton), new[] { "ProgressBarBackground", "ProgressBarForeground" },
            window => new MGProgressButton(window, new Thickness(1), MGUniformBorderBrush.Black),
            element =>
            {
                MGProgressButton button = (MGProgressButton)element;
                RecordingFillBrush[] recorders = Recorders(2);
                button.ProgressBarBackground = recorders[0];
                button.ProgressBarForeground = recorders[1];
                return recorders;
            }),
        new(typeof(MGProgressBar), new[] { "CompletedBrush", "IncompleteBrush" },
            window => new MGProgressBar(window),
            element =>
            {
                MGProgressBar progressBar = (MGProgressBar)element;
                RecordingFillBrush[] recorders = Recorders(2);
                progressBar.CompletedBrush = new VisualStateFillBrush(recorders[0]);
                progressBar.IncompleteBrush = new VisualStateFillBrush(recorders[1]);
                return recorders;
            }),
        new(typeof(MGGridColorPicker), new[] { "HoveredColorOverlay", "SelectedColorOverlay" },
            window => new MGGridColorPicker(window, 2, Color.Red, Color.Blue, Color.Green, Color.White),
            element =>
            {
                MGGridColorPicker picker = (MGGridColorPicker)element;
                RecordingFillBrush[] recorders = Recorders(2);
                picker.HoveredColorOverlay = recorders[0];
                picker.SelectedColorOverlay = recorders[1];
                return recorders;
            }),
        new(typeof(MGOverlayHost), new[] { "OverlayBackground" },
            window => new MGOverlayHost(window),
            element =>
            {
                RecordingFillBrush[] recorders = Recorders(1);
                ((MGOverlayHost)element).OverlayBackground = recorders[0];
                return recorders;
            }),
        new(typeof(MGUniformGrid), new[] { "SelectionBackground", "SelectionOverlay", "HorizontalGridLineBrush", "VerticalGridLineBrush", "CellBackground" },
            window => new MGUniformGrid(window, 2, 2, new Size(20, 20)),
            element =>
            {
                MGUniformGrid grid = (MGUniformGrid)element;
                RecordingFillBrush[] recorders = Recorders(5);
                grid.SelectionBackground = recorders[0];
                grid.SelectionOverlay = recorders[1];
                grid.HorizontalGridLineBrush = recorders[2];
                grid.VerticalGridLineBrush = recorders[3];
                grid.CellBackground = new VisualStateFillBrush(recorders[4]);
                return recorders;
            }),
        new(typeof(MGGrid), new[] { "SelectionBackground", "SelectionOverlay", "HorizontalGridLineBrush", "VerticalGridLineBrush" },
            window =>
            {
                MGGrid grid = new(window);
                grid.AddRow(GridLength.CreateWeightedLength(1));
                grid.AddColumn(GridLength.CreateWeightedLength(1));
                return grid;
            },
            element =>
            {
                MGGrid grid = (MGGrid)element;
                RecordingFillBrush[] recorders = Recorders(4);
                grid.SelectionBackground = recorders[0];
                grid.SelectionOverlay = recorders[1];
                grid.HorizontalGridLineBrush = recorders[2];
                grid.VerticalGridLineBrush = recorders[3];
                return recorders;
            }),
        new(typeof(MGGridSplitter), new[] { "Foreground" },
            window => new MGGridSplitter(window),
            element =>
            {
                RecordingFillBrush[] recorders = Recorders(1);
                ((MGGridSplitter)element).Foreground = new VisualStateFillBrush(recorders[0]);
                return recorders;
            }),
    };

    /// <summary>Slots deliberately not returned by the hooks. Keys are "DeclaringType.Property"; values state the reason (see Docs/drawing-architecture.md, Limites connues).</summary>
    private static readonly IReadOnlyDictionary<string, string> DocumentedExclusions = new Dictionary<string, string>
    {
        //  PROXY: the property forwards to a child element's BackgroundBrush, which that child already ticks.
        ["MGSpoiler.UnspoiledBackgroundBrush"] = "proxy of ButtonElement.BackgroundBrush (MGSpoiler.cs)",
        ["MGTabControl.HeaderAreaBackground"] = "proxy of HeadersPanelElement.BackgroundBrush (MGTabControl.cs)",
        ["MGExpander.ExpanderButtonBackgroundBrush"] = "proxy of ExpanderToggleButton.BackgroundBrush (MGExpander.cs)",
        ["MGToggleButton.CheckedBackgroundBrush"] = "proxy of BackgroundBrush.SelectedValue (MGToggleButton.cs)",
        //  TEMPLATE consumed by other elements: assigned into the BackgroundBrush of several consumer elements, so the slot itself is excluded here
        //  (it is ticked through each consumer's own GetVisualStateFillBrushes()/BackgroundBrush, not through this property). Since ADR-0005/S5
        //  (SetBackground subscribes the holder to its container's PropertyChanged, so a long-lived shared container would root every holder), the two
        //  VisualStateFillBrush templates below are handed to each consumer as a Copy(): each consumer ticks its own copy, the template instance itself
        //  is never ticked. The AlternatingRowBackgrounds brushes are still shared by reference into each row's NormalValue slot, where PaintLifecycle
        //  dedups them by reference against MGDesktop's per-frame registry (Docs/drawing-architecture.md, Limites connues).
        ["MGTreeView.SelectionBackgroundBrush"] = "template copied into HeaderPanel, HeaderContainer and the expander of the selected item (one Copy() per consumer since ADR-0005/S5), each copy ticked by its holder (MGTreeViewItem.cs RefreshSelectionVisual)",
        ["MGListBox.AlternatingRowBackgrounds"] = "template assigned to each row's ContentPresenter background, deduped to 1 tick/frame per distinct brush by MGDesktop's paint registry (MGListBox.cs)",
        ["MGDockAutoHideStrip.ButtonBackgroundBrush"] = "template copied into every strip button (one Copy() per button since ADR-0005/S5), each copy ticked by its holder (MGDockAutoHideStrip.cs ApplyThemeVisuals)",
        //  TEMPLATE current value only: only the brush of the current state is copied into SurfaceElement.BackgroundBrush.NormalValue, so non-current brushes are not ticked.
        ["MGDockSplitterBar.NormalBrush"] = "ticked only while it is the current state (MGDockSplitterBar.cs)",
        ["MGDockSplitterBar.HoverBrush"] = "ticked only while it is the current state (MGDockSplitterBar.cs)",
        ["MGDockSplitterBar.PressedBrush"] = "ticked only while it is the current state (MGDockSplitterBar.cs)",
        ["MGDockTabItem.NormalBrush"] = "ticked only while it is the current state (MGDockTabItem.cs)",
        ["MGDockTabItem.HoverBrush"] = "ticked only while it is the current state (MGDockTabItem.cs)",
        ["MGDockTabItem.ActiveBrush"] = "ticked only while it is the current state (MGDockTabItem.cs)",
        //  COLOUR ONLY: never drawn as a brush, MGGraphSurfaceCanvas.ResolveBrushColor extracts a colour, so ticking would have no effect.
        ["MGGraphView.GridLineBrush"] = "colour extracted by MGGraphSurfaceCanvas.ResolveBrushColor",
        ["MGGraphView.MajorGridLineBrush"] = "colour extracted by MGGraphSurfaceCanvas.ResolveBrushColor",
        ["MGGraphView.EdgeBrush"] = "colour extracted by MGGraphSurfaceCanvas.ResolveBrushColor",
    };

    private static RecordingFillBrush[] Recorders(int count) => Enumerable.Range(0, count).Select(_ => new RecordingFillBrush()).ToArray();

    public sealed record DirectSlotCase(Type ElementType, IReadOnlyList<string> SlotNames, Func<MGWindow, MGElement> Create, Func<MGElement, IReadOnlyList<RecordingFillBrush>> Inject)
    {
        public string Name => ElementType.Name;
        public override string ToString() => Name;
    }

    #endregion

    #region Test doubles and harness

    public sealed class RecordingFillBrush : IFillBrush
    {
        public int UpdateCount { get; private set; }
        public void Reset() => UpdateCount = 0;
        public void Update(UpdateBaseArgs UA) => UpdateCount++;
        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds) { }
        public IFillBrush Copy() => this;
    }

    /// <summary>Minimal <see cref="IPaintUpdateRegistry"/> double used to assert that <see cref="UpdateBaseArgs.GetTranslated"/>
    /// carries the registry instance through unchanged.</summary>
    private sealed class FakeRegistry : IPaintUpdateRegistry
    {
        public bool TryBeginUpdate(object paint) => true;
    }

    private sealed class RecordingBorderBrush : IBorderBrush
    {
        public int UpdateCount { get; private set; }
        public void Reset() => UpdateCount = 0;
        public void Update(UpdateBaseArgs UA) => UpdateCount++;
        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT) { }
        public IBorderBrush Copy() => this;
    }

    private sealed class HookProbeBorder : MGBorder
    {
        public HookProbeBorder(MGWindow window) : base(window) { }
        public IEnumerable<IFillBrush> FillBrushes => GetFillBrushes();
        public IEnumerable<VisualStateFillBrush> VisualStateFillBrushes => GetVisualStateFillBrushes();
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            runtime.ApplyFrame(OneFrame);
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0)
            };
            return new(runtime, desktop, window);
        }

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout and theme application are settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            Desktop.Windows.Add(Window);
            Desktop.Update();
            Desktop.Update();
        }

        /// <summary>Adds every element to a stack panel set as the window's content, shows the window and runs two warm-up frames.
        /// Used by tests that need several distinct elements updated within the same <see cref="MGDesktop.Update"/> call.</summary>
        public void ShowAll(params MGElement[] elements)
        {
            MGStackPanel panel = new(Window, Orientation.Vertical);
            foreach (MGElement element in elements)
            {
                panel.TryAddChild(element);
            }
            Show(panel);
        }
    }

    #endregion
}
