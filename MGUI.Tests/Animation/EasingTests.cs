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

    // --- U1: cubic Bezier easings (Docs/Tasks/animation-v3-tasks.md, ADR-0008 decision 1) ---

    /// <summary>Dense sampling of the parametric Bezier curve: an oracle independent of <see cref="UICubicBezierEasing.Ease"/>.</summary>
    private static float SampleOracle(float x1, float y1, float x2, float y2, float t)
    {
        const int steps = 100_000;
        float bestDistance = float.MaxValue;
        float bestY = 0f;
        for (int i = 0; i <= steps; i++)
        {
            float u = (float)i / steps;
            float x = BezierComponent(x1, x2, u);
            float distance = MathF.Abs(x - t);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestY = BezierComponent(y1, y2, u);
            }
        }

        return bestY;
    }

    private static float BezierComponent(float p1, float p2, float u)
    {
        float c = 3f * p1;
        float b = 3f * (p2 - p1) - c;
        float a = 1f - c - b;
        return ((a * u + b) * u + c) * u;
    }

    public static IEnumerable<object[]> CssPresets() => new[]
    {
        new object[] { 0.25f, 0.1f, 0.25f, 1f, 0.8024f },
        new object[] { 0.42f, 0f, 1f, 1f, 0.3153f },
        new object[] { 0f, 0f, 0.58f, 1f, 0.6847f },
        new object[] { 0.42f, 0f, 0.58f, 1f, 0.5f },
    };

    [Theory]
    [MemberData(nameof(CssPresets))]
    public void Bezier_MatchesTheCssPresetReferenceValues_AtEndpointsAndMidpoint(float x1, float y1, float x2, float y2, float expectedAtHalf)
    {
        UICubicBezierEasing easing = new(x1, y1, x2, y2);

        Assert.Equal(0f, easing.Ease(0f));
        Assert.Equal(1f, easing.Ease(1f));
        Assert.Equal(expectedAtHalf, easing.Ease(0.5f), 2e-3f);
        Assert.Equal(SampleOracle(x1, y1, x2, y2, 0.5f), easing.Ease(0.5f), 1e-3f);
    }

    [Theory]
    [InlineData(0.42f, 0f, 1f, 1f)]
    [InlineData(0.1f, 0.25f, 0.5f, 0.75f)]
    [InlineData(0.9f, 0.5f, 0.1f, 0f)]
    public void Bezier_MatchesTheOracle_AtSeveralPoints(float x1, float y1, float x2, float y2)
    {
        UICubicBezierEasing easing = new(x1, y1, x2, y2);
        foreach (float t in new[] { 0.1f, 0.25f, 0.5f, 0.75f, 0.9f })
        {
            Assert.Equal(SampleOracle(x1, y1, x2, y2, t), easing.Ease(t), 1e-3f);
        }
    }

    public static IEnumerable<object[]> NearBoundaryCurves() => new[]
    {
        new object[] { new UICubicBezierEasing(0.42f, 0f, 1f, 1f), true },
        new object[] { new UICubicBezierEasing(0.5f, 0f, 0.5f, 1f), false },
        new object[] { new UICubicBezierEasing(0f, 1f, 1f, 0f), false },
    };

    [Theory]
    [MemberData(nameof(NearBoundaryCurves))]
    public void Bezier_ConvergesNearTheBounds_AndMatchesTheOracle(UICubicBezierEasing easing, bool mustBeMonotonic)
    {
        float[] ts = { 1e-4f, 1e-3f, 0.01f, 0.99f, 0.999f, 1f - 1e-4f };
        float previous = float.NegativeInfinity;
        foreach (float t in ts)
        {
            float y = easing.Ease(t);
            Assert.True(float.IsFinite(y));
            Assert.InRange(y, -0.01f, 1.01f);
            Assert.Equal(SampleOracle(easing.X1, easing.Y1, easing.X2, easing.Y2, t), y, 1e-3f);

            if (mustBeMonotonic)
            {
                Assert.True(y >= previous - 1e-4f);
                previous = y;
            }
        }
    }

    [Fact]
    public void Bezier_IsMonotonic_ForEaseIn_OverManySamples()
    {
        UICubicBezierEasing easeIn = new(0.42f, 0f, 1f, 1f);
        float previous = float.NegativeInfinity;
        for (int i = 0; i <= 1000; i++)
        {
            float t = i / 1000f;
            float y = easeIn.Ease(t);
            Assert.True(float.IsFinite(y));
            Assert.True(y >= previous - 1e-4f);
            previous = y;
        }
    }

    [Fact]
    public void Bezier_Constructor_ThrowsOnOutOfRangeOrNonFiniteInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UICubicBezierEasing(-0.1f, 0f, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UICubicBezierEasing(1.5f, 0f, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UICubicBezierEasing(0.5f, 0f, -0.1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UICubicBezierEasing(0.5f, 0f, 1.1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UICubicBezierEasing(float.NaN, 0f, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UICubicBezierEasing(0.5f, float.PositiveInfinity, 1f, 1f));
    }

    [Fact]
    public void Bezier_Constructor_AllowsOvershootInY()
    {
        UICubicBezierEasing easing = new(0.5f, -0.5f, 0.5f, 1.5f);
        Assert.Equal(-0.5f, easing.Y1);
        Assert.Equal(1.5f, easing.Y2);
    }

    [Theory]
    [InlineData("cubic-bezier(0.42,0,1,1)")]
    [InlineData("Cubic-Bezier( 0.42 , 0 , 1 , 1 )")]
    [InlineData("bezier:0.42,0,1,1")]
    [InlineData("bezier: 0.42, 0, 1, 1")]
    public void TryParse_AcceptsBothFormsWithTolerantWhitespaceAndCasing(string text)
    {
        Assert.True(UICubicBezierEasing.TryParse(text, out UICubicBezierEasing easing));
        Assert.Equal(0.42f, easing.X1, Tolerance);
        Assert.Equal(0f, easing.Y1, Tolerance);
        Assert.Equal(1f, easing.X2, Tolerance);
        Assert.Equal(1f, easing.Y2, Tolerance);
        Assert.Equal("cubic-bezier(0.42,0,1,1)", easing.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cubic-bezier(0.42,0,1)")]
    [InlineData("cubic-bezier(a,b,c,d)")]
    [InlineData("cubic-bezier(0.42,0,1,1")]
    [InlineData("bezier:1.5,0,1,1")]
    [InlineData("cubic-bezier(-0.1,0,1,1)")]
    [InlineData("ease")]
    [InlineData("cubic-bezier(0.42,0,1,1,2)")]
    [InlineData("CUBIC-BEZIER(0.1,0.2,0.3,0.4,0.5)")]
    [InlineData("bezier:1e-1,0,1,1,2")]
    [InlineData("bezier:0,42,0,1,1")]
    [InlineData("cubic-bezier(0.5,NaN,0.5,1)")]
    [InlineData("cubic-bezier(0.5,1,0.5,NaN)")]
    [InlineData("cubic-bezier(0.5,Infinity,0.5,1)")]
    [InlineData("cubic-bezier(0.5,-Infinity,0.5,1)")]
    [InlineData("bezier:0.5,NaN,0.5,1")]
    [InlineData("cubic-bezier(0.5,1e40,0.5,1)")]
    public void TryParse_RefusesMalformedOrOutOfRangeText(string text)
    {
        Assert.False(UICubicBezierEasing.TryParse(text, out UICubicBezierEasing easing));
        Assert.Null(easing);
    }

    [Theory]
    [InlineData("cubic-bezier(0.5,NaN,0.5,1)")]
    [InlineData("cubic-bezier(0.5,Infinity,0.5,1)")]
    [InlineData("cubic-bezier(0.5,-Infinity,0.5,1)")]
    public void TryGet_RejectsANonFiniteYBezierLiteral_WithoutThrowing(string literal)
    {
        Assert.False(UIEasing.TryGet(literal, out IUIEasingFunction function));
        Assert.Null(function);
    }

    [Fact]
    public void Parse_ThrowsFormatExceptionOnNonFiniteY_NotArgumentOutOfRangeException()
    {
        Assert.Throws<FormatException>(() => UICubicBezierEasing.Parse("cubic-bezier(0.5,NaN,0.5,1)"));
    }

    [Fact]
    public void TryParse_AcceptsOvershootInY()
    {
        Assert.True(UICubicBezierEasing.TryParse("cubic-bezier(0.5,-0.5,0.5,1.5)", out UICubicBezierEasing easing));
        Assert.Equal(-0.5f, easing.Y1);
        Assert.Equal(1.5f, easing.Y2);
    }

    [Fact]
    public void TryParse_AcceptsExponentNotation()
    {
        Assert.True(UICubicBezierEasing.TryParse("cubic-bezier(1e-1,0,1,1)", out UICubicBezierEasing easing));
        Assert.Equal(0.1f, easing.X1, Tolerance);
    }

    [Fact]
    public void TryParse_TrimsLeadingAndTrailingWhitespace()
    {
        Assert.True(UICubicBezierEasing.TryParse("  cubic-bezier(0.42,0,1,1)  ", out UICubicBezierEasing easing));
        Assert.Equal(0.42f, easing.X1, Tolerance);
    }

    [Fact]
    public void Parse_ThrowsFormatExceptionListingTheAcceptedSyntaxes()
    {
        FormatException error = Assert.Throws<FormatException>(() => UICubicBezierEasing.Parse("nope"));
        Assert.Contains("cubic-bezier", error.Message);
        Assert.Contains("bezier:", error.Message);
    }

    [Fact]
    public void TryGet_ResolvesAndCachesABezierLiteral_AndListsItInNames()
    {
        string literal = "cubic-bezier(0.13,0.37,0.91,0.59)";

        Assert.True(UIEasing.TryGet(literal, out IUIEasingFunction first));
        Assert.IsType<UICubicBezierEasing>(first);
        Assert.True(UIEasing.TryGet(literal, out IUIEasingFunction second));
        Assert.Same(first, second);
        Assert.Contains(literal, UIEasing.Names);

        Assert.True(UIEasing.TryGet("Linear", out IUIEasingFunction linear));
        Assert.Same(UIEasing.Linear, linear);
        Assert.True(UIEasing.TryGet("cubicout", out IUIEasingFunction cubicOut));
        Assert.Same(UIEasing.CubicOut, cubicOut);
    }

    [Fact]
    public void TryGet_RejectsAnOutOfRangeBezierLiteral_WithoutAddingItToNames()
    {
        string literal = "cubic-bezier(2,0,1,1)";

        Assert.False(UIEasing.TryGet(literal, out IUIEasingFunction function));
        Assert.Null(function);
        Assert.DoesNotContain(literal, UIEasing.Names);
    }

    [Fact]
    public void Bezier_Ease_DoesNotAllocate()
    {
        UICubicBezierEasing easing = new(0.42f, 0f, 1f, 1f);
        float sink = 0f;
        for (int i = 0; i < 16; i++)
        {
            sink += easing.Ease(0.3f);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            sink += easing.Ease(0.1f);
            sink += easing.Ease(0.5f);
            sink += easing.Ease(0.9f);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.NotEqual(float.NaN, sink);
    }
}
