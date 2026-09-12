using Microsoft.Xna.Framework;
using MGUI.Core.UI.Animation.Interpolation;
using MonoGame.Extended;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

public class InterpolatorTests
{
    private const float Tolerance = 1e-5f;

    [Theory]
    [InlineData(0f, 10f, 0f, 0f)]
    [InlineData(0f, 10f, 0.25f, 2.5f)]
    [InlineData(0f, 10f, 1f, 10f)]
    [InlineData(10f, 0f, 0.5f, 5f)]
    public void Float_InterpolatesLinearly(float from, float to, float amount, float expected)
    {
        Assert.Equal(expected, UIFloatInterpolator.Instance.Lerp(from, to, amount), Tolerance);
    }

    [Fact]
    public void Float_DoesNotClampOvershoot()
    {
        Assert.Equal(12f, UIFloatInterpolator.Instance.Lerp(0f, 10f, 1.2f), Tolerance);
        Assert.Equal(-5f, UIFloatInterpolator.Instance.Lerp(0f, 10f, -0.5f), Tolerance);
    }

    [Fact]
    public void Double_InterpolatesLinearly_WithoutClamping()
    {
        Assert.Equal(2.5, UIDoubleInterpolator.Instance.Lerp(0.0, 10.0, 0.25f), 1e-9);
        Assert.Equal(12.0, UIDoubleInterpolator.Instance.Lerp(0.0, 10.0, 1.2f), 1e-6);
    }

    [Theory]
    [InlineData(0, 1, 0.5f, 1)]     // 0.5 rounds away from zero
    [InlineData(0, 3, 0.5f, 2)]     // 1.5 -> 2
    [InlineData(0, -3, 0.5f, -2)]   // -1.5 -> -2
    [InlineData(10, 20, 0.25f, 13)] // 12.5 -> 13
    [InlineData(10, 20, 0f, 10)]
    [InlineData(10, 20, 1f, 20)]
    public void Int_RoundsAwayFromZero(int from, int to, float amount, int expected)
    {
        Assert.Equal(expected, UIIntInterpolator.Instance.Lerp(from, to, amount));
    }

    [Fact]
    public void NullableInt_InterpolatesWhenBothSidesHaveValues()
    {
        Assert.Equal(15, UINullableIntInterpolator.Instance.Lerp(10, 20, 0.5f));
    }

    [Theory]
    [InlineData(0.49f, false)]
    [InlineData(0.5f, true)]
    [InlineData(1f, true)]
    public void NullableInt_SwitchesAtMidpoint_WhenFromIsNull(float amount, bool expectsTo)
    {
        int? result = UINullableIntInterpolator.Instance.Lerp(null, 10, amount);

        Assert.Equal(expectsTo ? 10 : null, result);
    }

    [Fact]
    public void NullableInt_SwitchesAtMidpoint_WhenToIsNull()
    {
        Assert.Equal(10, UINullableIntInterpolator.Instance.Lerp(10, null, 0.49f));
        Assert.Null(UINullableIntInterpolator.Instance.Lerp(10, null, 0.5f));
    }

    [Fact]
    public void Vectors_InterpolateEachComponent()
    {
        Assert.Equal(new Vector2(5f, 10f), UIVector2Interpolator.Instance.Lerp(Vector2.Zero, new Vector2(10f, 20f), 0.5f));
        Assert.Equal(new Vector3(5f, 10f, 15f), UIVector3Interpolator.Instance.Lerp(Vector3.Zero, new Vector3(10f, 20f, 30f), 0.5f));
        Assert.Equal(new Vector4(5f, 10f, 15f, 20f), UIVector4Interpolator.Instance.Lerp(Vector4.Zero, new Vector4(10f, 20f, 30f, 40f), 0.5f));
        Assert.Equal(new Vector2(12f, 24f), UIVector2Interpolator.Instance.Lerp(Vector2.Zero, new Vector2(10f, 20f), 1.2f));
    }

    [Fact]
    public void Color_InterpolatesChannels_AndClampsAmount()
    {
        Color mid = UIColorInterpolator.Instance.Lerp(Color.Black, Color.White, 0.5f);

        Assert.InRange(mid.R, 127, 128);
        Assert.Equal(mid.R, mid.G);
        Assert.Equal(mid.R, mid.B);
        Assert.Equal(255, mid.A);
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), mid);
        Assert.Equal(Color.White, UIColorInterpolator.Instance.Lerp(Color.Black, Color.White, 2f));
        Assert.Equal(Color.Black, UIColorInterpolator.Instance.Lerp(Color.Black, Color.White, -1f));
    }

    [Fact]
    public void Color_InterpolatesAlpha()
    {
        Color transparentRed = new(255, 0, 0, 0);
        Color mid = UIColorInterpolator.Instance.Lerp(transparentRed, Color.Red, 0.5f);

        Assert.InRange(mid.A, 127, 128);
    }

    [Fact]
    public void Rectangle_InterpolatesPositionAndSize()
    {
        Rectangle result = UIRectangleInterpolator.Instance.Lerp(new Rectangle(0, 0, 10, 10), new Rectangle(10, 20, 30, 50), 0.5f);

        Assert.Equal(new Rectangle(5, 10, 20, 30), result);
    }

    [Theory]
    [InlineData(0, 1, 0.5f, 1)]
    [InlineData(0, 3, 0.5f, 2)]
    [InlineData(0, -3, 0.5f, -2)]
    [InlineData(4, 8, 0.25f, 5)]
    public void Thickness_RoundsEachSideAwayFromZero(int from, int to, float amount, int expected)
    {
        Thickness result = UIThicknessInterpolator.Instance.Lerp(new Thickness(from), new Thickness(to), amount);

        Assert.Equal(expected, result.Left);
        Assert.Equal(expected, result.Top);
        Assert.Equal(expected, result.Right);
        Assert.Equal(expected, result.Bottom);
    }

    [Fact]
    public void Thickness_InterpolatesSidesIndependently()
    {
        Thickness result = UIThicknessInterpolator.Instance.Lerp(new Thickness(0, 10, 20, 30), new Thickness(10, 10, 0, 40), 0.5f);

        Assert.Equal(5, result.Left);
        Assert.Equal(10, result.Top);
        Assert.Equal(10, result.Right);
        Assert.Equal(35, result.Bottom);
    }

    [Fact]
    public void Registry_ProvidesEveryBuiltIn()
    {
        Assert.True(UIInterpolators.TryGet(out IUIInterpolator<float> f));
        Assert.Same(UIFloatInterpolator.Instance, f);
        Assert.True(UIInterpolators.IsRegistered<double>());
        Assert.True(UIInterpolators.IsRegistered<int>());
        Assert.True(UIInterpolators.IsRegistered<int?>());
        Assert.True(UIInterpolators.IsRegistered<Vector2>());
        Assert.True(UIInterpolators.IsRegistered<Vector3>());
        Assert.True(UIInterpolators.IsRegistered<Vector4>());
        Assert.True(UIInterpolators.IsRegistered<Color>());
        Assert.True(UIInterpolators.IsRegistered<Rectangle>());
        Assert.True(UIInterpolators.IsRegistered<Thickness>());
    }

    [Fact]
    public void Registry_ReportsUnknownType_WithAnExplicitMessage()
    {
        Assert.False(UIInterpolators.TryGet(out IUIInterpolator<decimal> interpolator));
        Assert.Null(interpolator);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => UIInterpolators.Get<decimal>());

        Assert.Contains(typeof(decimal).FullName, error.Message);
        Assert.Contains(nameof(UIInterpolators.Register), error.Message);
    }

    private readonly record struct CustomValue(float A, float B);

    private sealed class CustomValueInterpolator : IUIInterpolator<CustomValue>
    {
        public CustomValue Lerp(CustomValue from, CustomValue to, float amount)
            => new(from.A + (to.A - from.A) * amount, from.B + (to.B - from.B) * amount);
    }

    [Fact]
    public void Registry_AcceptsAnApplicationType()
    {
        Assert.False(UIInterpolators.IsRegistered<CustomValue>());

        UIInterpolators.Register(new CustomValueInterpolator());

        Assert.True(UIInterpolators.TryGet(out IUIInterpolator<CustomValue> interpolator));
        Assert.Equal(new CustomValue(5f, 50f), interpolator.Lerp(new CustomValue(0f, 0f), new CustomValue(10f, 100f), 0.5f));
        Assert.Same(interpolator, UIInterpolators.Get<CustomValue>());
    }

    [Fact]
    public void Registry_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => UIInterpolators.Register<float>(null));
    }

    [Fact]
    public void Lerp_DoesNotAllocate()
    {
        IUIInterpolator<float> floats = UIInterpolators.Get<float>();
        IUIInterpolator<Color> colors = UIInterpolators.Get<Color>();
        IUIInterpolator<Thickness> thicknesses = UIInterpolators.Get<Thickness>();
        float sink = 0f;
        Thickness thickness = new(1);

        // Warm up (JIT, static constructors).
        for (int i = 0; i < 16; i++)
        {
            sink += floats.Lerp(0f, 1f, 0.5f) + colors.Lerp(Color.Black, Color.White, 0.5f).R;
            thickness = thicknesses.Lerp(thickness, new Thickness(i), 0.5f);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            sink += floats.Lerp(0f, 1f, 0.5f) + colors.Lerp(Color.Black, Color.White, 0.5f).R;
            thickness = thicknesses.Lerp(thickness, new Thickness(i), 0.5f);
            UIInterpolators.TryGet(out IUIInterpolator<float> _);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.NotEqual(float.NaN, sink + thickness.Left);
    }
}
