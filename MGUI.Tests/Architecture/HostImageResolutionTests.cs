using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Assets;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Behavioural coverage of ADR-0016's "Host resolution of image names" and "SourceName without event churn":
/// an <see cref="MGImage"/> whose <see cref="MGImage.SourceName"/> is unknown to <see cref="MGResources"/> gets its image
/// from the host's <see cref="IUIAssetProvider"/>, and changing <see cref="MGImage.SourceName"/> subscribes to
/// <see cref="MGResources.OnTextureAdded"/>/<see cref="MGResources.OnTextureRemoved"/> at most once.</summary>
public class HostImageResolutionTests
{
    [Fact]
    public void SourceName_UnknownToResources_ResolvesThroughProvider_OncePerName_EvenAcrossSeveralImagesAndChanges()
    {
        FakeResolvingAssetProvider provider = new();
        FakeImageResource fakeImage = new(32, 32);
        Rectangle sourceRect = new(4, 4, 16, 16);
        provider.Register("sprite:hero", fakeImage, sourceRect);

        Harness harness = Harness.Create(provider);

        MGImage image1 = new(harness.Window, "sprite:hero");
        MGImage image2 = new(harness.Window, "sprite:hero");
        harness.Panel.TryAddChild(image1);
        harness.Panel.TryAddChild(image2);
        harness.Desktop.Update();
        harness.Desktop.Update();

        Assert.NotNull(image1.ActualSource);
        Assert.Same(fakeImage, image1.ActualSource!.Value.Image);
        Assert.Equal(sourceRect, image1.ActualSource!.Value.SourceRect);
        Assert.NotNull(image2.ActualSource);
        Assert.Same(fakeImage, image2.ActualSource!.Value.Image);
        Assert.Equal(1, provider.AskCount("sprite:hero"));

        // Changing SourceName away and back, and re-assigning the same already-resolved name, must not ask again.
        image1.SourceName = null;
        image1.SourceName = "sprite:hero";
        image2.SourceName = "sprite:hero";
        harness.Desktop.Update();

        Assert.Equal(1, provider.AskCount("sprite:hero"));
    }

    [Fact]
    public void SourceName_UnresolvableByProvider_LeavesActualSourceNull_AndIsAskedOncePerRoot()
    {
        FakeResolvingAssetProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image1 = new(harness.Window, "sprite:missing");
        MGImage image2 = new(harness.Window, "sprite:missing");
        harness.Panel.TryAddChild(image1);
        harness.Panel.TryAddChild(image2);

        Exception exception = Record.Exception(() => harness.Desktop.Update());
        Assert.Null(exception);

        Assert.Null(image1.ActualSource);
        Assert.Null(image2.ActualSource);
        Assert.Equal(1, provider.AskCount("sprite:missing"));

        // A repeated lookup for the same unresolvable name must not ask the provider again.
        image1.SourceName = null;
        image1.SourceName = "sprite:missing";
        harness.Desktop.Update();

        Assert.Equal(1, provider.AskCount("sprite:missing"));
    }

    [Fact]
    public void SourceName_TextureAddedLater_RefreshesActualSource_AndInvalidatesLayout()
    {
        FakeResolvingAssetProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "sprite:lazy");
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();
        harness.Desktop.Update();

        Assert.Null(image.ActualSource);
        int widthBeforeAdd = image.ActualLayoutBounds.Width;
        int heightBeforeAdd = image.ActualLayoutBounds.Height;

        FakeImageResource lateImage = new(48, 24);
        // Same scope the image itself resolves against (its window's resource scope), so this exercises the
        // OnTextureAdded notification path directly rather than the cross-scope provider-resolution path.
        image.GetResources().AddTexture("sprite:lazy", new MGTextureData(lateImage));

        // Defect 1 (Resources_OnTextureAddedRemoved must compare e.Name, not the element's own Name): the image
        // must refresh from the OnTextureAdded notification alone, without any further nudge.
        Assert.NotNull(image.ActualSource);
        Assert.Same(lateImage, image.ActualSource!.Value.Image);

        // Defect 2 (UpdateActualSource must go through the ActualSource setter, not the backing field): the size
        // change must have invalidated layout, so measured size follows the new texture after Update() settles.
        harness.Desktop.Update();
        harness.Desktop.Update();

        // Stretch.Uniform (the default) scales to fit the available space while keeping the 48x24 (2:1) aspect
        // ratio, so assert on that ratio and on the size actually having changed, rather than on raw pixel dimensions.
        int widthAfterAdd = image.ActualLayoutBounds.Width;
        int heightAfterAdd = image.ActualLayoutBounds.Height;
        Assert.True(widthAfterAdd > 0 && heightAfterAdd > 0);
        Assert.True(widthAfterAdd != widthBeforeAdd || heightAfterAdd != heightBeforeAdd);
        Assert.Equal(2.0, widthAfterAdd / (double)heightAfterAdd, 1);
    }

    [Fact]
    public void SourceName_ChangesBetweenAlreadyResolvedNames_AllocateNothing()
    {
        FakeImageResource imageA = new(16, 16);
        FakeImageResource imageB = new(16, 16);
        FakeResolvingAssetProvider provider = new();
        Harness harness = Harness.Create(provider);
        harness.Desktop.Resources.AddTexture("a", new MGTextureData(imageA));
        harness.Desktop.Resources.AddTexture("b", new MGTextureData(imageB));

        MGImage image = new(harness.Window, "a");
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();
        harness.Desktop.Update();

        void Toggle(int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                image.SourceName = (i % 2 == 0) ? "b" : "a";
            }
        }

        Toggle(50); // Warm-up: JIT the setter path, populate the cached PropertyChangedEventArgs.

        long before = GC.GetAllocatedBytesForCurrentThread();
        Toggle(1000);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void ForgetHostResolvedTextures_AsksProviderAgain_ButLeavesExplicitlyAddedTexturesAndFiresRemoval()
    {
        FakeResolvingAssetProvider provider = new();
        FakeImageResource resolvedImage = new(32, 32);
        FakeImageResource explicitImage = new(8, 8);
        provider.Register("sprite:hero", resolvedImage);

        Harness harness = Harness.Create(provider);
        harness.Desktop.Resources.AddTexture("explicit:name", new MGTextureData(explicitImage));

        MGImage resolvedByProvider = new(harness.Window, "sprite:hero");
        MGImage unresolvable = new(harness.Window, "sprite:missing");
        harness.Panel.TryAddChild(resolvedByProvider);
        harness.Panel.TryAddChild(unresolvable);
        harness.Desktop.Update();
        harness.Desktop.Update();

        Assert.NotNull(resolvedByProvider.ActualSource);
        Assert.Equal(1, provider.AskCount("sprite:hero"));
        Assert.Equal(1, provider.AskCount("sprite:missing"));

        var removedNames = new List<string>();
        harness.Desktop.Resources.OnTextureRemoved += (_, e) => removedNames.Add(e.Name);

        harness.Desktop.Resources.ForgetHostResolvedTextures();

        // The provider-resolved texture was removed (raising OnTextureRemoved); the explicitly added one was not.
        Assert.Contains("sprite:hero", removedNames);
        Assert.DoesNotContain("explicit:name", removedNames);
        Assert.True(harness.Desktop.Resources.TryGetTexture("explicit:name", out _));

        // Both the positive and the negative cache were forgotten: a fresh lookup asks the provider again for
        // each name (the "sprite:hero" MGImage already triggered its own re-lookup from the removal event
        // above; "sprite:missing" never got one, since nothing was removed for a name that never resolved).
        harness.Desktop.Resources.TryGetTexture("sprite:hero", out _);
        harness.Desktop.Resources.TryGetTexture("sprite:missing", out _);
        Assert.Equal(2, provider.AskCount("sprite:hero"));
        Assert.Equal(2, provider.AskCount("sprite:missing"));
    }

    private sealed class FakeResolvingAssetProvider : IUIAssetProvider
    {
        private readonly Dictionary<string, (IUIImageResource Image, Rectangle? SourceRect)> _registrations = new();
        private readonly Dictionary<string, int> _askCounts = new();

        public void Register(string name, IUIImageResource image, Rectangle? sourceRect = null) => _registrations[name] = (image, sourceRect);

        public int AskCount(string name) => _askCounts.GetValueOrDefault(name);

        public IUIImageResource LoadImage(string assetName) => throw new NotSupportedException("Not used by these tests.");

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = null!;
            return false;
        }

        public bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)
        {
            _askCounts[name] = _askCounts.GetValueOrDefault(name) + 1;

            if (_registrations.TryGetValue(name, out var entry))
            {
                image = entry.Image;
                sourceRect = entry.SourceRect;
                return true;
            }

            image = null!;
            sourceRect = null;
            return false;
        }
    }

    private sealed class FakeImageResource : IUIImageResource
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;

        public FakeImageResource(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGStackPanel Panel)
    {
        public static Harness Create(IUIAssetProvider provider)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540), provider);
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
            };
            MGStackPanel panel = new(window, Orientation.Vertical);
            window.SetContent(panel);
            desktop.Windows.Add(window);
            desktop.Update();

            return new(runtime, desktop, window, panel);
        }
    }
}
