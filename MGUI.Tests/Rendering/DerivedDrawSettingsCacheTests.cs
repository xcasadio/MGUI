using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Rendering;

/// <summary>A draw transaction keeps the settings it derived for a temporary transform or effect (ADR-0031): the same change finds the same
/// settings object, from settings that are the same object or equal ones (ADR-0029).</summary>
public class DerivedDrawSettingsCacheTests
{
    private static readonly Matrix Moved = Matrix.CreateTranslation(3, 4, 0);
    private static readonly Matrix Scaled = Matrix.CreateScale(2);

    [Fact]
    public void WithTransform_DerivesTheSettings_AndFindsThemAgain()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Matrix.Identity, RasterizerType.Solid, BlendType: BlendType.Additive);

        DrawSettings derived = cache.WithTransform(source, Moved);

        Assert.Equal(source with { Transform = Moved }, derived);
        Assert.Same(derived, cache.WithTransform(source, Moved));
        Assert.Same(derived, cache.WithTransform(source with { }, Moved));
        Assert.NotSame(derived, cache.WithTransform(source, Scaled));
        Assert.NotSame(derived, cache.WithTransform(source with { BlendType = BlendType.Opaque }, Moved));
        Assert.Equal(BlendType.Opaque, cache.WithTransform(source with { BlendType = BlendType.Opaque }, Moved).BlendType);
    }

    [Fact]
    public void WithTransform_OfTheTransformTheSettingsHave_IsTheSettings()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Moved);

        Assert.Same(source, cache.WithTransform(source, Moved));
    }

    [Fact]
    public void WithEffect_DerivesTheSettings_AndFindsThemAgain()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Moved);
        object effect = new();
        object otherEffect = new();

        DrawSettings derived = cache.WithEffect(source, effect);

        Assert.Same(effect, derived.BackendEffect);
        Assert.Equal(Moved, derived.Transform);
        Assert.Same(derived, cache.WithEffect(source, effect));
        Assert.Same(derived, cache.WithEffect(source with { }, effect));
        Assert.NotSame(derived, cache.WithEffect(source, otherEffect));
        Assert.Same(source, cache.WithEffect(source, null));
        Assert.Same(derived, cache.WithEffect(derived, effect));
        Assert.Null(cache.WithEffect(derived, null).BackendEffect);
    }

    [Fact]
    public void ATransformAndAnEffect_DoNotFindEachOther()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Matrix.Identity);
        object effect = new();

        DrawSettings withEffect = cache.WithEffect(source, effect);
        DrawSettings withTransform = cache.WithTransform(source, default);

        Assert.Null(withTransform.BackendEffect);
        Assert.Equal(default, withTransform.Transform);
        Assert.Same(effect, withEffect.BackendEffect);
        Assert.Equal(Matrix.Identity, withEffect.Transform);
    }

    [Fact]
    public void NestedChanges_FindTheirSettingsAgain()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Matrix.Identity);

        DrawSettings outer = cache.WithTransform(source, Moved);
        DrawSettings inner = cache.WithTransform(outer, Scaled);

        Assert.Same(inner, cache.WithTransform(cache.WithTransform(new DrawSettings(Matrix.Identity), Moved), Scaled));
    }

    [Fact]
    public void MoreValuesThanTheCapacity_ReplaceTheOldest_AndStayCorrect()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Matrix.Identity);
        DrawSettings first = cache.WithTransform(source, Matrix.CreateTranslation(1, 0, 0));

        for (int i = 2; i <= DerivedDrawSettingsCache.Capacity + 1; i++)
        {
            Assert.Equal(Matrix.CreateTranslation(i, 0, 0), cache.WithTransform(source, Matrix.CreateTranslation(i, 0, 0)).Transform);
        }

        DrawSettings again = cache.WithTransform(source, Matrix.CreateTranslation(1, 0, 0));
        Assert.NotSame(first, again);
        Assert.Equal(first, again);
        Assert.Same(again, cache.WithTransform(source, Matrix.CreateTranslation(1, 0, 0)));
    }

    [Fact]
    public void Clear_ForgetsTheEntries()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings source = new(Matrix.Identity);
        DrawSettings derived = cache.WithTransform(source, Moved);

        cache.Clear();

        DrawSettings again = cache.WithTransform(source, Moved);
        Assert.NotSame(derived, again);
        Assert.Equal(derived, again);
    }

    [Fact]
    public void AChangeThatWasDerivedBefore_AllocatesNothing()
    {
        DerivedDrawSettingsCache cache = new();
        DrawSettings[] sources = { new(Matrix.Identity), new(Matrix.Identity) };
        object effect = new();
        cache.WithEffect(cache.WithTransform(cache.WithTransform(sources[0], Moved), Scaled), effect);

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            DrawSettings outer = cache.WithTransform(sources[i & 1], Moved);
            DrawSettings inner = cache.WithTransform(outer, Scaled);
            cache.WithEffect(inner, effect);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
