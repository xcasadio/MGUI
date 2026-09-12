using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Tests.Animation;

public class EasingTests
{
    private const float Tolerance = 1e-5f;

    private static readonly string[] BuiltInNames =
    {
        "Linear",
        "QuadIn", "QuadOut", "QuadInOut",
        "CubicIn", "CubicOut", "CubicInOut",
        "SineIn", "SineOut", "SineInOut",
        "BackIn", "BackOut", "BackInOut",
        "BounceIn", "BounceOut", "BounceInOut",
        "ElasticIn", "ElasticOut", "ElasticInOut",
    };

    public static IEnumerable<object[]> EveryBuiltIn() => BuiltInNames.Select(name => new object[] { name });

    public static IEnumerable<object[]> EveryFamily() => new[] { "Quad", "Cubic", "Sine", "Back", "Bounce", "Elastic" }.Select(f => new object[] { f });

    private static IUIEasingFunction Get(string name)
    {
        Assert.True(UIEasing.TryGet(name, out IUIEasingFunction function), $"'{name}' is not registered");
        return function;
    }

    [Theory]
    [MemberData(nameof(EveryBuiltIn))]
    public void EveryFunction_MapsEndpoints(string name)
    {
        IUIEasingFunction function = Get(name);

        Assert.Equal(0f, function.Ease(0f), Tolerance);
        Assert.Equal(1f, function.Ease(1f), Tolerance);
    }

    [Theory]
    [MemberData(nameof(EveryBuiltIn))]
    public void EveryFunction_IsRegisteredUnderItsFieldName(string name)
    {
        IUIEasingFunction function = Get(name);
        IUIEasingFunction field = (IUIEasingFunction)typeof(UIEasing).GetField(name).GetValue(null);

        Assert.Same(field, function);
        Assert.Equal(name, function.ToString());
    }

    [Fact]
    public void Names_ListsTheNineteenBuiltIns()
    {
        Assert.Equal(19, BuiltInNames.Length);
        Assert.True(BuiltInNames.All(UIEasing.Names.Contains));
    }

    [Theory]
    [InlineData("Linear", 0.5f, 0.5f)]
    [InlineData("Linear", 1.5f, 1.5f)]
    [InlineData("QuadIn", 0.5f, 0.25f)]
    [InlineData("QuadOut", 0.5f, 0.75f)]
    [InlineData("CubicIn", 0.5f, 0.125f)]
    [InlineData("CubicOut", 0.5f, 0.875f)]
    [InlineData("SineIn", 0.5f, 0.29289323f)]
    [InlineData("SineOut", 0.5f, 0.70710677f)]
    [InlineData("BounceOut", 0.5f, 0.765625f)]
    public void KnownValues(string name, float amount, float expected)
    {
        Assert.Equal(expected, Get(name).Ease(amount), Tolerance);
    }

    [Theory]
    [MemberData(nameof(EveryFamily))]
    public void InOut_PassesThroughTheMidpoint(string family)
    {
        Assert.Equal(0.5f, Get(family + "InOut").Ease(0.5f), Tolerance);
    }

    [Theory]
    [MemberData(nameof(EveryFamily))]
    public void In_IsTheMirrorOfOut(string family)
    {
        IUIEasingFunction @in = Get(family + "In");
        IUIEasingFunction @out = Get(family + "Out");

        foreach (float x in new[] { 0.1f, 0.25f, 0.4f, 0.6f, 0.75f, 0.9f })
        {
            Assert.Equal(@in.Ease(x), 1f - @out.Ease(1f - x), 1e-4f);
        }
    }

    [Fact]
    public void Back_UndershootsIn_AndOvershootsOut()
    {
        Assert.True(UIEasing.BackIn.Ease(0.2f) < 0f);
        Assert.True(UIEasing.BackOut.Ease(0.5f) > 1f);
    }

    [Fact]
    public void Elastic_Overshoots()
    {
        Assert.True(UIEasing.ElasticOut.Ease(0.1f) > 1f);
        Assert.True(UIEasing.ElasticIn.Ease(0.9f) < 0f);
    }

    [Fact]
    public void Bounce_StaysWithinRange()
    {
        for (float x = 0f; x <= 1f; x += 0.05f)
        {
            Assert.InRange(UIEasing.BounceOut.Ease(x), 0f, 1f + Tolerance);
            Assert.InRange(UIEasing.BounceIn.Ease(x), -Tolerance, 1f);
        }
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        Assert.True(UIEasing.TryGet("cubicout", out IUIEasingFunction lower));
        Assert.True(UIEasing.TryGet("CUBICOUT", out IUIEasingFunction upper));

        Assert.Same(UIEasing.CubicOut, lower);
        Assert.Same(UIEasing.CubicOut, upper);
    }

    [Fact]
    public void TryGet_RejectsUnknownAndNull()
    {
        Assert.False(UIEasing.TryGet("Nope", out IUIEasingFunction unknown));
        Assert.Null(unknown);
        Assert.False(UIEasing.TryGet(null, out IUIEasingFunction fromNull));
        Assert.Null(fromNull);
    }

    private sealed class StepEasing : IUIEasingFunction
    {
        public float Ease(float amount) => amount < 0.5f ? 0f : 1f;
    }

    [Fact]
    public void Register_AcceptsAnApplicationFunction()
    {
        StepEasing step = new();

        UIEasing.Register("StepTest", step);

        Assert.True(UIEasing.TryGet("steptest", out IUIEasingFunction registered));
        Assert.Same(step, registered);
        Assert.Contains("StepTest", UIEasing.Names);
    }

    [Fact]
    public void Register_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => UIEasing.Register(" ", UIEasing.Linear));
        Assert.Throws<ArgumentNullException>(() => UIEasing.Register("Null", null));
    }

    [Fact]
    public void Ease_DoesNotAllocate()
    {
        float sink = 0f;
        for (int i = 0; i < 16; i++)
        {
            foreach (string name in BuiltInNames)
            {
                sink += Get(name).Ease(0.3f);
            }
        }

        IUIEasingFunction[] functions = BuiltInNames.Select(Get).ToArray();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            foreach (IUIEasingFunction function in functions)
            {
                sink += function.Ease(0.3f);
            }
            UIEasing.TryGet("CubicOut", out IUIEasingFunction _);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.NotEqual(float.NaN, sink);
    }
}
