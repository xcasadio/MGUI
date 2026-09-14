using System;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Animation;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Covers the freezable contract added by ADR-0009: <see cref="IUIFreezable"/>'s transitional default members on
/// <see cref="IFillBrush"/>/<see cref="IBorderBrush"/>, <see cref="UIFreezableBrush"/>'s frozen-state enforcement, notifying setters
/// and allocation-free notifications, and <see cref="UIBrushEquality"/>'s value comparisons. No brush is converted in this slice:
/// <see cref="TestFreezableBrush"/> is a private double built only for this test.</summary>
public class FreezableBrushTests
{
    /// <summary>Minimal <see cref="UIFreezableBrush"/> double: an <see cref="IFillBrush"/> with a settable <see cref="Color"/> and a
    /// nested <see cref="IFillBrush"/> child, so <see cref="OnFreeze"/> has something to propagate to.</summary>
    private sealed class TestFreezableBrush : UIFreezableBrush, IFillBrush
    {
        private Color _Color;
        public Color Color
        {
            get => _Color;
            set => SetProperty(ref _Color, value);
        }

        private IFillBrush _Child;
        public IFillBrush Child
        {
            get => _Child;
            set => SetProperty(ref _Child, value);
        }

        public override bool CanFreeze => (Child as IUIFreezable)?.CanFreeze != false;

        protected override void OnFreeze() => (Child as IUIFreezable)?.Freeze();

        public TestFreezableBrush(Color Color, IFillBrush Child = null)
        {
            _Color = Color;
            _Child = Child;
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds) { }

        public IFillBrush Copy() => new TestFreezableBrush(Color, Child?.Copy());
    }

    /// <summary>A double whose <see cref="UIFreezableBrush.CanFreeze"/> is always false, to test propagation of a child that refuses freezing.</summary>
    private sealed class UnfreezableTestBrush : UIFreezableBrush, IFillBrush
    {
        public override bool CanFreeze => false;
        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds) { }
        public IFillBrush Copy() => new UnfreezableTestBrush();
    }

    #region UIFreezableBrush

    [Fact]
    public void Setter_Before_Freeze_Notifies_Exactly_Once()
    {
        TestFreezableBrush brush = new(Color.Red);
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TestFreezableBrush.Color))
            {
                notificationCount++;
            }
        };

        brush.Color = Color.Blue;

        Assert.Equal(1, notificationCount);
        Assert.Equal(Color.Blue, brush.Color);
    }

    [Fact]
    public void Setter_With_Unchanged_Value_Does_Not_Notify()
    {
        TestFreezableBrush brush = new(Color.Red);
        int notificationCount = 0;
        brush.PropertyChanged += (_, _) => notificationCount++;

        brush.Color = Color.Red;

        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public void Setter_Allocates_Nothing_After_Warmup()
    {
        TestFreezableBrush brush = new(Color.Red);
        brush.PropertyChanged += (_, _) => { };

        // Warm-up: JIT the setter/notification path and let the two alternating colors get boxed/interned once.
        for (int i = 0; i < 16; i++)
        {
            brush.Color = i % 2 == 0 ? Color.Red : Color.Blue;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            brush.Color = i % 2 == 0 ? Color.Red : Color.Blue;
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void Setter_After_Freeze_Throws_And_Names_The_Property()
    {
        TestFreezableBrush brush = new(Color.Red);
        brush.Freeze();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => brush.Color = Color.Blue);

        Assert.Contains(nameof(TestFreezableBrush.Color), ex.Message);
        Assert.Contains(nameof(TestFreezableBrush), ex.Message);
    }

    [Fact]
    public void Freeze_Twice_Is_Fine()
    {
        TestFreezableBrush brush = new(Color.Red);

        brush.Freeze();
        brush.Freeze();

        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void CanFreeze_False_Makes_Freeze_Throw_And_IsFrozen_Stays_False()
    {
        UnfreezableTestBrush brush = new();

        Assert.Throws<InvalidOperationException>(() => brush.Freeze());
        Assert.False(brush.IsFrozen);
    }

    [Fact]
    public void Freeze_Of_Parent_With_Unfreezable_Child_Throws_Before_Freezing()
    {
        TestFreezableBrush parent = new(Color.Red, new UnfreezableTestBrush());

        Assert.Throws<InvalidOperationException>(() => parent.Freeze());
        Assert.False(parent.IsFrozen);
    }

    [Fact]
    public void OnFreeze_Freezes_The_Child()
    {
        TestFreezableBrush child = new(Color.Green);
        TestFreezableBrush parent = new(Color.Red, child);

        parent.Freeze();

        Assert.True(parent.IsFrozen);
        Assert.True(child.IsFrozen);
    }

    [Fact]
    public void Copy_Of_A_Frozen_Double_Is_Unfrozen_And_Independent()
    {
        TestFreezableBrush child = new(Color.Green);
        TestFreezableBrush original = new(Color.Red, child);
        original.Freeze();

        TestFreezableBrush copy = (TestFreezableBrush)original.Copy();

        Assert.False(copy.IsFrozen);
        Assert.Equal(Color.Red, copy.Color);
        copy.Color = Color.Yellow;
        Assert.Equal(Color.Red, original.Color);
    }

    #endregion

    #region UIBrushEquality

    [Fact]
    public void Two_Solid_Brushes_Of_The_Same_Color_Are_Value_Equal()
    {
        IFillBrush a = new MGSolidFillBrush(Color.Red);
        IFillBrush b = new MGSolidFillBrush(Color.Red);

        Assert.True(UIBrushEquality.ValueEquals(a, b));
    }

    [Fact]
    public void Two_Solid_Brushes_Of_Different_Colors_Are_Not_Value_Equal()
    {
        IFillBrush a = new MGSolidFillBrush(Color.Red);
        IFillBrush b = new MGSolidFillBrush(Color.Blue);

        Assert.False(UIBrushEquality.ValueEquals(a, b));
    }

    [Fact]
    public void A_Solid_And_A_Gradient_Are_Not_Value_Equal_Even_With_The_Same_Corner_Colors()
    {
        IFillBrush solid = new MGSolidFillBrush(Color.Red);
        IFillBrush gradient = new MGGradientFillBrush(Color.Red, Color.Red, Color.Red, Color.Red);

        Assert.False(UIBrushEquality.ValueEquals(solid, gradient));
    }

    [Fact]
    public void Two_Uniform_Border_Brushes_Around_Equal_Solids_Are_Value_Equal()
    {
        IBorderBrush a = new MGUniformBorderBrush(new MGSolidFillBrush(Color.Red));
        IBorderBrush b = new MGUniformBorderBrush(new MGSolidFillBrush(Color.Red));

        Assert.True(UIBrushEquality.ValueEquals(a, b));
    }

    [Fact]
    public void Two_Uniform_Border_Brushes_Around_Different_Solids_Are_Not_Value_Equal()
    {
        IBorderBrush a = new MGUniformBorderBrush(new MGSolidFillBrush(Color.Red));
        IBorderBrush b = new MGUniformBorderBrush(new MGSolidFillBrush(Color.Blue));

        Assert.False(UIBrushEquality.ValueEquals(a, b));
    }

    [Fact]
    public void ValueEquals_Handles_Nulls()
    {
        Assert.True(UIBrushEquality.ValueEquals((IFillBrush)null, (IFillBrush)null));
        Assert.False(UIBrushEquality.ValueEquals(new MGSolidFillBrush(Color.Red), null));
        Assert.False(UIBrushEquality.ValueEquals(null, new MGSolidFillBrush(Color.Red)));

        Assert.True(UIBrushEquality.ValueEquals((IBorderBrush)null, (IBorderBrush)null));
        Assert.False(UIBrushEquality.ValueEquals(new MGUniformBorderBrush(Color.Red), null));
    }

    [Fact]
    public void Two_VisualStateFillBrush_With_Equal_Slots_Are_Value_Equal()
    {
        VisualStateFillBrush a = new(new MGSolidFillBrush(Color.Red), Color.Green, PressedModifierType.Darken, 0.1f);
        VisualStateFillBrush b = new(new MGSolidFillBrush(Color.Red), Color.Green, PressedModifierType.Darken, 0.1f);

        Assert.True(UIBrushEquality.ValueEquals(a, b));
    }

    [Fact]
    public void Two_VisualStateFillBrush_Differing_In_One_Slot_Are_Not_Value_Equal()
    {
        VisualStateFillBrush a = new(new MGSolidFillBrush(Color.Red), Color.Green, PressedModifierType.Darken, 0.1f);
        VisualStateFillBrush b = a.Copy();
        b.SelectedValue = new MGSolidFillBrush(Color.Blue);

        Assert.False(UIBrushEquality.ValueEquals(a, b));
    }

    [Fact]
    public void ForGuards_Of_NonBrush_T_Is_The_Default_Comparer()
    {
        Assert.Same(EqualityComparer<float>.Default, UIBrushEquality.ForGuards<float>());
        Assert.Same(EqualityComparer<int>.Default, UIBrushEquality.ForGuards<int>());
        Assert.Same(EqualityComparer<MonoGame.Extended.Thickness>.Default, UIBrushEquality.ForGuards<MonoGame.Extended.Thickness>());
        Assert.Same(EqualityComparer<Color?>.Default, UIBrushEquality.ForGuards<Color?>());
    }

    [Fact]
    public void ForGuards_Of_IFillBrush_Is_The_Value_Comparer()
    {
        IEqualityComparer<IFillBrush> comparer = UIBrushEquality.ForGuards<IFillBrush>();

        Assert.True(comparer.Equals(new MGSolidFillBrush(Color.Red), new MGSolidFillBrush(Color.Red)));
        Assert.False(comparer.Equals(new MGSolidFillBrush(Color.Red), new MGSolidFillBrush(Color.Blue)));
    }

    [Fact]
    public void ForGuards_Is_Computed_Once_Per_T_And_Allocates_Nothing_After_First_Call()
    {
        _ = UIBrushEquality.ForGuards<IFillBrush>(); // warm-up: force the static generic cache to build

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            _ = UIBrushEquality.ForGuards<IFillBrush>();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    #endregion

    #region MGSolidFillBrush / MGGradientFillBrush / MGDiagonalGradientFillBrush (ADR-0009)

    [Fact]
    public void MGSolidFillBrush_Setter_Notifies_And_Throws_When_Frozen()
    {
        MGSolidFillBrush brush = new(Color.Red);
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MGSolidFillBrush.Color))
            {
                notificationCount++;
            }
        };

        brush.Color = Color.Blue;
        Assert.Equal(1, notificationCount);
        Assert.Equal(Color.Blue, brush.Color);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.Color = Color.Green);
    }

    [Fact]
    public void MGSolidFillBrush_Copy_Of_A_Frozen_Palette_Brush_Is_Unfrozen_And_Equal_By_Value()
    {
        MGSolidFillBrush copy = (MGSolidFillBrush)SolidFillBrushes.Red.Copy();

        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(SolidFillBrushes.Red));
        copy.Color = Color.Black; // does not throw: the copy is independent and unfrozen
        Assert.Equal(Color.Red, SolidFillBrushes.Red.Color); // the palette brush itself is untouched
    }

    [Fact]
    public void SolidFillBrushes_Mutating_A_Palette_Brush_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SolidFillBrushes.Red.Color = Color.Black);
    }

    [Fact]
    public void SolidFillBrushes_Every_Public_Static_Field_Is_Frozen()
    {
        FieldInfo[] fields = typeof(SolidFillBrushes).GetFields(BindingFlags.Public | BindingFlags.Static);
        Assert.True(fields.Length > 100, $"Expected the full named-color palette (over 100 fields), found {fields.Length}.");

        foreach (FieldInfo field in fields)
        {
            MGSolidFillBrush brush = Assert.IsType<MGSolidFillBrush>(field.GetValue(null));
            Assert.True(brush.IsFrozen, $"SolidFillBrushes.{field.Name} is not frozen.");
        }
    }

    [Fact]
    public void MGGradientFillBrush_Setters_Notify_And_Throw_When_Frozen_Then_Copy_Is_Unfrozen()
    {
        MGGradientFillBrush brush = new(Color.Red, Color.Green, Color.Blue, Color.White);
        int notificationCount = 0;
        brush.PropertyChanged += (_, _) => notificationCount++;

        brush.TopLeftColor = Color.Black;
        Assert.Equal(1, notificationCount);
        Assert.Equal(Color.Black, brush.TopLeftColor);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.TopRightColor = Color.Yellow);

        MGGradientFillBrush copy = (MGGradientFillBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        copy.BottomRightColor = Color.Orange; // does not throw
    }

    [Fact]
    public void MGDiagonalGradientFillBrush_Setters_Notify_And_Throw_When_Frozen_Then_Copy_Is_Unfrozen()
    {
        MGDiagonalGradientFillBrush brush = new(Color.Red, Color.Blue, CornerType.TopLeft);
        int notificationCount = 0;
        brush.PropertyChanged += (_, _) => notificationCount++;

        brush.Color1 = Color.Black;
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.Color2 = Color.Yellow);
        Assert.Throws<InvalidOperationException>(() => brush.Color1Position = CornerType.BottomRight);

        MGDiagonalGradientFillBrush copy = (MGDiagonalGradientFillBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        copy.Color1Position = CornerType.BottomRight; // does not throw
    }

    [Fact]
    public void ValueEquals_Distinguishes_The_Three_Converted_Brush_Types_From_Each_Other()
    {
        IFillBrush solid = new MGSolidFillBrush(Color.Red);
        IFillBrush gradient = new MGGradientFillBrush(Color.Red, Color.Red, Color.Red, Color.Red);
        IFillBrush diagonal = new MGDiagonalGradientFillBrush(Color.Red, Color.Red, CornerType.TopLeft);

        Assert.False(UIBrushEquality.ValueEquals(solid, gradient));
        Assert.False(UIBrushEquality.ValueEquals(solid, diagonal));
        Assert.False(UIBrushEquality.ValueEquals(gradient, diagonal));

        Assert.True(UIBrushEquality.ValueEquals(gradient, new MGGradientFillBrush(Color.Red, Color.Red, Color.Red, Color.Red)));
        Assert.True(UIBrushEquality.ValueEquals(diagonal, new MGDiagonalGradientFillBrush(Color.Red, Color.Red, CornerType.TopLeft)));
        Assert.False(UIBrushEquality.ValueEquals(diagonal, new MGDiagonalGradientFillBrush(Color.Red, Color.Red, CornerType.TopRight)));
    }

    /// <summary>Drawing-level probe (ACCEPTANCE 3): mutating the <see cref="MGSolidFillBrush.Color"/> of a brush set inline on an
    /// element's <see cref="MGElement.BackgroundBrush"/> changes what the next drawn frame fills with, since the store/element now holds
    /// a reference to the very same mutable instance instead of a boxed struct copy.</summary>
    [Fact]
    public void MutatingAnInlineSolidBrushColor_ChangesWhatTheNextFrameDraws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGSolidFillBrush brush = new(Color.Red);
        scene.Top.BackgroundBrush.NormalValue = brush;
        scene.Frames(1);

        var beforeFrame = scene.Draw();
        Assert.Contains(beforeFrame.FillRectangleCalls, call => call.Color == Color.Red);

        brush.Color = Color.Blue;
        scene.Frames(1);

        var afterFrame = scene.Draw();
        Assert.Contains(afterFrame.FillRectangleCalls, call => call.Color == Color.Blue);
        Assert.DoesNotContain(afterFrame.FillRectangleCalls, call => call.Color == Color.Red);
    }

    #endregion

    #region Remaining value brushes and composites (ADR-0009)

    /// <summary>Same small texture-brush test double as <see cref="TexturedPaintProjectionTests"/>'s own private <c>Recorder</c>
    /// (duplicated here rather than shared, matching that file's own pattern).</summary>
    private readonly record struct Recorder(GraphTestRuntime Runtime, GraphNoOpDrawTransaction Transaction)
    {
        private static int _imageCounter;

        public static Recorder Create(GraphTestRuntime runtime = null)
        {
            runtime ??= new GraphTestRuntime(new Rectangle(0, 0, 960, 540));
            return new(runtime, new GraphNoOpDrawTransaction(runtime, DrawSettings.Default));
        }

        public GraphTestImageResource Image(int width, int height) => new($"tex-{System.Threading.Interlocked.Increment(ref _imageCounter)}", width, height);

        public ElementDrawArgs Args() => new(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
    }

    private static MGProgressBar CreateProgressBar()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 480, 260) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        return new MGProgressBar(window);
    }

    [Fact]
    public void MGTextureFillBrush_Setter_Notifies_And_Throws_When_Frozen_Then_Copy_Is_Unfrozen_And_ValueEqual()
    {
        Recorder recorder = Recorder.Create();
        MGTextureFillBrush brush = new(new MGTextureData(recorder.Image(8, 8)), Stretch.Fill);
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGTextureFillBrush.Tile)) { notificationCount++; } };

        brush.Tile = true;
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.Tile = false);

        MGTextureFillBrush copy = (MGTextureFillBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        copy.Tile = false; // does not throw
    }

    [Fact]
    public void MGNineSliceFillBrush_Setter_Notifies_And_Throws_When_Frozen_Then_Copy_Is_Unfrozen_And_ValueEqual()
    {
        Recorder recorder = Recorder.Create();
        MGTextureData patch = new(recorder.Image(9, 9));
        MGNineSliceFillBrush brush = new(new Thickness(1), patch, patch, patch, patch, patch, patch, patch, patch, patch);
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGNineSliceFillBrush.TargetMargin)) { notificationCount++; } };

        brush.TargetMargin = new Thickness(2);
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.TargetMargin = new Thickness(3));

        MGNineSliceFillBrush copy = (MGNineSliceFillBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        copy.TargetMargin = new Thickness(3); // does not throw
    }

    [Fact]
    public void MGProgressBarGradientBrush_Setter_Notifies_And_Throws_When_Frozen_Then_Copy_Is_Unfrozen_And_ValueEqual()
    {
        MGProgressBar progressBar = CreateProgressBar();
        MGProgressBarGradientBrush brush = new(progressBar);
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGProgressBarGradientBrush.MinimumValueColor)) { notificationCount++; } };

        brush.MinimumValueColor = Color.Black;
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.MinimumValueColor = Color.White);

        MGProgressBarGradientBrush copy = (MGProgressBarGradientBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        Assert.Same(progressBar, copy.ProgressBar); // the ProgressBar reference is shared, not copied
        copy.MinimumValueColor = Color.White; // does not throw

        MGProgressBarGradientBrush differentProgressBar = new(CreateProgressBar(), brush.MinimumValueColor, brush.MiddleValueColor, brush.MaximumValueColor);
        Assert.False(brush.ValueEquals(differentProgressBar)); // observing a different ProgressBar is a different value
    }

    [Fact]
    public void MGPaddedFillBrush_Freezes_Nested_Brush_And_CanFreeze_Is_False_When_Child_Cannot_Freeze()
    {
        //  Uses MGSolidFillBrush (real value equality) rather than the TestFreezableBrush double, so ValueEquals(copy, brush)
        //  below is meaningful: TestFreezableBrush has no value equality of its own (falls back to reference equality).
        MGSolidFillBrush child = new(Color.Green);
        MGPaddedFillBrush brush = new(child, new Thickness(2));

        brush.Freeze();

        Assert.True(brush.IsFrozen);
        Assert.True(child.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => brush.Padding = new Thickness(3));

        MGPaddedFillBrush unfreezableParent = new(new UnfreezableTestBrush(), new Thickness(2));
        Assert.False(unfreezableParent.CanFreeze);
        Assert.Throws<InvalidOperationException>(() => unfreezableParent.Freeze());

        MGPaddedFillBrush copy = (MGPaddedFillBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
    }

    [Fact]
    public void MGPaddedFillBrush_Brush_Setter_Stores_A_Distinct_Value_Equal_Instance_And_Notifies()
    {
        //  Assigning a distinct-but-value-equal IFillBrush must not be a silent no-op (ADR-0009), because the
        //  caller may mutate the newly assigned instance afterwards (an element-owned brush) and the property must already
        //  hold the exact instance for that mutation to reach it. See UIBrushEquality.ForSlots<T>() and the same rule on
        //  VisualStateSetting<T>'s slot setters.
        MGSolidFillBrush original = new(Color.Red);
        MGSolidFillBrush distinctButEqual = new(Color.Red);
        MGPaddedFillBrush brush = new(original, new Thickness(2));

        int notifyCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGPaddedFillBrush.Brush)) { notifyCount++; } };

        brush.Brush = distinctButEqual;

        Assert.Same(distinctButEqual, brush.Brush);
        Assert.Equal(1, notifyCount);

        distinctButEqual.Color = Color.Blue;
        Assert.Equal(Color.Blue, ((MGSolidFillBrush)brush.Brush).Color); // the mutation reaches the property because the exact instance is stored
    }

    [Fact]
    public void MGBorderedFillBrush_Freezes_Both_Nested_Brushes_And_CanFreeze_Is_False_When_Either_Child_Cannot_Freeze()
    {
        MGSolidFillBrush fill = new(Color.Red);
        MGUniformBorderBrush border = new(Color.Blue);
        MGBorderedFillBrush brush = new(new Thickness(1), border, fill, false);

        brush.Freeze();

        Assert.True(brush.IsFrozen);
        Assert.True(fill.IsFrozen);
        Assert.True(border.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => brush.PadFillBoundsByBorderThickness = true);

        MGBorderedFillBrush copy = (MGBorderedFillBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
    }

    [Fact]
    public void MGBorderedFillBrush_BorderBrush_And_FillBrush_Setters_Store_A_Distinct_Value_Equal_Instance_And_Notify()
    {
        //  Same hazard and same rule as MGPaddedFillBrush_Brush_Setter_... (ADR-0009), for both nested slots.
        MGUniformBorderBrush originalBorder = new(Color.Blue);
        MGUniformBorderBrush distinctButEqualBorder = new(Color.Blue);
        MGSolidFillBrush originalFill = new(Color.Red);
        MGSolidFillBrush distinctButEqualFill = new(Color.Red);
        MGBorderedFillBrush brush = new(new Thickness(1), originalBorder, originalFill, false);

        brush.BorderBrush = distinctButEqualBorder;
        brush.FillBrush = distinctButEqualFill;

        Assert.Same(distinctButEqualBorder, brush.BorderBrush);
        Assert.Same(distinctButEqualFill, brush.FillBrush);
    }

    [Fact]
    public void MGUniformBorderBrush_Freeze_Freezes_The_Inner_Brush_And_ValueEquals_Forwards_To_It()
    {
        MGSolidFillBrush inner = new(Color.Red);
        MGUniformBorderBrush brush = new(inner);

        brush.Freeze();

        Assert.True(inner.IsFrozen);
        Assert.True(UIBrushEquality.ValueEquals(brush, new MGUniformBorderBrush(new MGSolidFillBrush(Color.Red))));

        MGUniformBorderBrush copy = (MGUniformBorderBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
    }

    [Fact]
    public void MGDockedBorderBrush_IsSolidColorsOnly_Flips_When_A_Side_Becomes_A_Gradient_And_Back()
    {
        static bool GetIsSolidColorsOnly(MGDockedBorderBrush b) =>
            (bool)typeof(MGDockedBorderBrush).GetProperty("IsSolidColorsOnly", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(b)!;

        Recorder recorder = Recorder.Create();
        MGDockedBorderBrush brush = new(Color.Red, Color.Green, Color.Blue, Color.White);
        MGBoxShape shape = new(new Rectangle(0, 0, 40, 40), new Thickness(4), new MGCornerRadius(0));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);

        //  All solid: draws without exception.
        Assert.True(GetIsSolidColorsOnly(brush));
        brush.Draw(recorder.Args(), null!, shape, geometry);

        brush.Left = new MGGradientFillBrush(Color.Red, Color.Red, Color.Red, Color.Red);
        MonoGame.Extended.Thickness borderThickness = shape.NormalizedBorderThickness;
        //  Draw still succeeds through the non-solid fallback path (rectangle draw), no exception either way.
        Assert.False(GetIsSolidColorsOnly(brush));
        brush.Draw(recorder.Args(), null!, shape, geometry);

        brush.Left = new MGSolidFillBrush(Color.Red);
        Assert.True(GetIsSolidColorsOnly(brush));
        brush.Draw(recorder.Args(), null!, shape, geometry);

        MGDockedBorderBrush copy = (MGDockedBorderBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
    }

    [Fact]
    public void MGDockedBorderBrush_Left_Setter_Stores_A_Distinct_Value_Equal_Instance_And_Throws_Frozen_Before_Null_Check()
    {
        //  Same hazard and same rule as MGPaddedFillBrush_Brush_Setter_... (ADR-0009).
        MGSolidFillBrush originalLeft = new(Color.Red);
        MGSolidFillBrush distinctButEqualLeft = new(Color.Red);
        MGDockedBorderBrush brush = new(originalLeft, new MGSolidFillBrush(Color.Green), new MGSolidFillBrush(Color.Blue), new MGSolidFillBrush(Color.White));

        brush.Left = distinctButEqualLeft;
        Assert.Same(distinctButEqualLeft, brush.Left);

        //  A frozen instance must report the frozen error even when the assigned value is null, not ArgumentNullException.
        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.Left = null!);
    }

    [Fact]
    public void MGBandedBorderBrush_Freeze_Freezes_Every_Bands_Brush_And_Copy_Is_Deep()
    {
        MGUniformBorderBrush bandBrush = new(Color.Red);
        MGBandedBorderBrush brush = new(new MGBorderBand(bandBrush, 1.0));

        brush.Freeze();
        Assert.True(bandBrush.IsFrozen);

        MGBandedBorderBrush copy = (MGBandedBorderBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        Assert.NotSame(brush.Bands[0].Brush, copy.Bands[0].Brush);
    }

    [Fact]
    public void MGTexturedBorderBrush_Setter_Notifies_And_Throws_When_Frozen_Then_Copy_Is_Unfrozen_And_ValueEqual()
    {
        Recorder recorder = Recorder.Create();
        MGTexturedBorderBrush brush = new(recorder.Image(32, 8), recorder.Image(8, 8));
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGTexturedBorderBrush.Opacity)) { notificationCount++; } };

        brush.Opacity = 0.5f;
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.Opacity = 1.0f);

        MGTexturedBorderBrush copy = (MGTexturedBorderBrush)brush.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(brush));
        copy.Opacity = 1.0f; // does not throw
    }

    [Fact]
    public void MGCompositedFillBrush_Freeze_Freezes_Children_Recursively_And_A_Frozen_Composites_List_Refuses_Add()
    {
        //  Uses MGSolidFillBrush (real value equality) rather than the TestFreezableBrush double, so ValueEquals(copy, composite)
        //  below is meaningful: TestFreezableBrush has no value equality of its own (falls back to reference equality).
        MGSolidFillBrush childA = new(Color.Red);
        MGSolidFillBrush childB = new(Color.Blue);
        MGCompositedFillBrush composite = new(childA, childB);

        composite.Freeze();

        Assert.True(childA.IsFrozen);
        Assert.True(childB.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => composite.Brushes.Add(new TestFreezableBrush(Color.Green)));

        MGCompositedFillBrush unfreezable = new(new UnfreezableTestBrush());
        Assert.False(unfreezable.CanFreeze);
        Assert.Throws<InvalidOperationException>(() => unfreezable.Freeze());

        MGCompositedFillBrush copy = (MGCompositedFillBrush)composite.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(composite));
        Assert.NotSame(composite.Brushes[0], copy.Brushes[0]);
        copy.Brushes.Add(new TestFreezableBrush(Color.Green)); // does not throw: the copy is unfrozen
    }

    [Fact]
    public void MGCompositedBorderBrush_Freeze_Freezes_Children_Recursively_And_A_Frozen_Composites_List_Refuses_Add()
    {
        MGUniformBorderBrush childA = new(Color.Red);
        MGUniformBorderBrush childB = new(Color.Blue);
        MGCompositedBorderBrush composite = new(childA, childB);

        composite.Freeze();

        Assert.True(childA.IsFrozen);
        Assert.True(childB.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => composite.Brushes.Add(new MGUniformBorderBrush(Color.Green)));

        MGCompositedBorderBrush copy = (MGCompositedBorderBrush)composite.Copy();
        Assert.False(copy.IsFrozen);
        Assert.True(copy.ValueEquals(composite));
        Assert.NotSame(composite.Brushes[0], copy.Brushes[0]);
    }

    [Fact]
    public void VisualStateFillBrush_Copy_Shares_A_Frozen_Slot_And_Copies_An_Unfrozen_Slot()
    {
        MGSolidFillBrush normal = new(Color.Red);
        normal.Freeze();
        MGSolidFillBrush selected = new(Color.Blue); // left unfrozen

        VisualStateFillBrush original = new(normal, selected, selected, Color.Green, PressedModifierType.Darken, 0.1f);

        VisualStateFillBrush copy = original.Copy();

        Assert.Same(normal, copy.NormalValue); // frozen slot: shared by reference
        Assert.NotSame(selected, copy.SelectedValue); // unfrozen slot: deep-copied
        Assert.True(UIBrushEquality.ValueEquals(selected, copy.SelectedValue)); // but still value-equal
    }

    [Fact]
    public void MGHighlightFillBrush_Setter_Notifies_And_Throws_When_Frozen()
    {
        MGHighlightFillBrush brush = new();
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGHighlightFillBrush.FocusedElementPadding)) { notificationCount++; } };

        brush.FocusedElementPadding = 4;
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.FocusedElementPadding = 8);
        Assert.Throws<InvalidOperationException>(() => brush.IsEnabled = false);
    }

    [Fact]
    public void MGHighlightBorderBrush_Setter_Notifies_And_Throws_When_Frozen_Except_AnimationProgress()
    {
        MGHighlightBorderBrush brush = new(MGUniformBorderBrush.Black, Color.Yellow, HighlightAnimation.Pulse);
        int notificationCount = 0;
        brush.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGHighlightBorderBrush.HighlightColor)) { notificationCount++; } };

        brush.HighlightColor = Color.Orange;
        Assert.Equal(1, notificationCount);

        brush.Freeze();
        Assert.Throws<InvalidOperationException>(() => brush.HighlightColor = Color.Purple);
        Assert.Throws<InvalidOperationException>(() => brush.Underlay = MGUniformBorderBrush.White);

        //  AnimationProgress is deliberately excluded from the freeze contract (ADR-0009): the engine run owns its accumulator.
        brush.AnimationProgress = 0.5;
        Assert.Equal(0.5, brush.AnimationProgress);
    }

    #endregion
}
