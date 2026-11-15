using System.Reflection;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Rendering;

/// <summary>A <see cref="DrawSettings"/> holds nothing but its parameters (ADR-0029): a backend may keep a settings object across frames and
/// compare it with a new one.</summary>
public class DrawSettingsEqualityTests
{
    [Fact]
    public void TwoDefaults_AreEqualAndNotTheSameObject()
    {
        DrawSettings first = DrawSettings.Default;
        DrawSettings second = DrawSettings.Default;

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ACopy_IsEqualToItsSource()
    {
        DrawSettings source = new(Matrix.CreateTranslation(7, 9, 0), RasterizerType.Solid, DrawSortMode.Immediate, BlendType.Additive,
            SamplerType.LinearWrap, DepthStencilType.StencilReadEqual, new object(), 3, 0x0F, 0xF0);

        Assert.Equal(source, source with { });
    }

    [Fact]
    public void ACopyWithAnotherTransform_EqualsNewSettingsWithThatTransform()
    {
        Matrix transform = Matrix.CreateScale(2f) * Matrix.CreateTranslation(5, 6, 0);

        Assert.Equal(new DrawSettings(transform), DrawSettings.Default with { Transform = transform });
        Assert.Equal(DrawSettings.Default, new DrawSettings(transform) with { Transform = Matrix.Identity });
    }

    [Fact]
    public void EachParameter_TakesPartInEquality()
    {
        DrawSettings reference = DrawSettings.Default;

        Assert.NotEqual(reference, reference with { Transform = Matrix.CreateTranslation(1, 0, 0) });
        Assert.NotEqual(reference, reference with { RasterizerType = RasterizerType.Solid });
        Assert.NotEqual(reference, reference with { Sort = DrawSortMode.Immediate });
        Assert.NotEqual(reference, reference with { BlendType = BlendType.Additive });
        Assert.NotEqual(reference, reference with { SamplerType = SamplerType.LinearWrap });
        Assert.NotEqual(reference, reference with { DepthStencilType = DepthStencilType.DepthRead });
        Assert.NotEqual(reference, reference with { BackendEffect = new object() });
        Assert.NotEqual(reference, reference with { StencilReference = 1 });
        Assert.NotEqual(reference, reference with { StencilReadMask = 0x0F });
        Assert.NotEqual(reference, reference with { StencilWriteMask = 0x0F });
    }

    /// <summary>The generated equality compares every instance field: a field that is not a parameter (a cache, a derived value) would make two
    /// settings with the same values differ, or go stale on a copy.</summary>
    [Fact]
    public void TheInstanceFields_AreTheParametersOnly()
    {
        FieldInfo[] fields = typeof(DrawSettings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        ParameterInfo[] parameters = typeof(DrawSettings).GetConstructors().Single(x => x.GetParameters().Length == 10).GetParameters();

        Assert.Equal(parameters.Length, fields.Length);
        foreach (ParameterInfo parameter in parameters)
        {
            Assert.Contains(fields, x => x.Name == $"<{parameter.Name}>k__BackingField");
        }
    }
}
