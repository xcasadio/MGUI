using System;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Animation;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>Covers the freezable contract added by ADR-0009 (W1): <see cref="IUIFreezable"/>'s transitional default members on
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

    #region MGSolidFillBrush / MGGradientFillBrush / MGDiagonalGradientFillBrush (ADR-0009, W2)

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
}
