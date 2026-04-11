using System.IO;
using System.Text.RegularExpressions;

namespace MGUI.Tests.Architecture;

public class Phase4RenderingArchitectureTests
{
    private static readonly string RepoRoot = @"d:\development\repo\MGUI";
    private static readonly string SharedRoot = Path.Combine(RepoRoot, "MGUI.Shared");
    private static readonly string CoreRoot = Path.Combine(RepoRoot, "MGUI.Core");

    private static readonly string[] ForbiddenMonoGameTokens =
    {
        "GraphicsDevice",
        "SpriteBatch",
        "PrimitiveBatch",
        "Texture2D",
        "RenderTarget2D",
        "ContentManager",
        "MainRenderer"
    };

    private static readonly HashSet<string> SharedContractLeakFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assets/IUIAssetProvider.cs",
        "Rendering/IUIDrawContext.cs",
        "Rendering/IUIRenderContext.cs",
        "Rendering/IUISurface.cs",
        "Text/Engines/IMonoGameTextRenderer.cs"
    };

    [Fact]
    public void MGUI_Shared_ForbiddenMonoGameTokenReferences_AreLimitedToKnownPhase4Files()
    {
        AssertTokenFiles(SharedRoot, "GraphicsDevice", new[]
        {
            "Helpers/ContentUtils.cs",
            "Helpers/RenderUtils.cs",
            "Helpers/TextureUtils.cs",
            "Rendering/Clipping/ClipManager.cs",
            "Rendering/DelegateRenderHost.cs",
            "Rendering/DrawTransaction.cs",
            "Rendering/IUIRenderContext.cs",
            "Rendering/MainRenderer.cs",
            "Rendering/RenderTargetPool.cs",
            "Rendering/View.cs"
        });

        AssertTokenFiles(SharedRoot, "SpriteBatch", new[]
        {
            "Helpers/TextureUtils.cs",
            "Rendering/DrawSettings.cs",
            "Rendering/DrawTransaction.cs",
            "Rendering/MainRenderer.cs",
            "Text/Engines/IMonoGameTextRenderer.cs",
            "Text/Engines/SpriteFontTextEngine.cs"
        });

        AssertTokenFiles(SharedRoot, "PrimitiveBatch", new[]
        {
            "Helpers/RenderUtils.cs",
            "Rendering/DrawTransaction.cs",
            "Rendering/MainRenderer.cs"
        });

        AssertTokenFiles(SharedRoot, "Texture2D", new[]
        {
            "Assets/IUIAssetProvider.cs",
            "Assets/MonoGameImageResource.cs",
            "Helpers/ContentUtils.cs",
            "Helpers/TextureUtils.cs",
            "Rendering/DrawTransaction.cs",
            "Rendering/IUIDrawContext.cs",
            "Rendering/MainRenderer.cs"
        });

        AssertTokenFiles(SharedRoot, "RenderTarget2D", new[]
        {
            "Helpers/RenderUtils.cs",
            "Helpers/TextureUtils.cs",
            "Rendering/BackBufferSurface.cs",
            "Rendering/Clipping/ClipManager.cs",
            "Rendering/DrawTransaction.cs",
            "Rendering/IUIRenderContext.cs",
            "Rendering/IUISurface.cs",
            "Rendering/RenderTargetPool.cs"
        });

        AssertTokenFiles(SharedRoot, "ContentManager", new[]
        {
            "Assets/IUIAssetProvider.cs",
            "Helpers/ContentUtils.cs",
            "Rendering/MainRenderer.cs",
            "Text/FontManager.cs",
            "Text/FontSet.cs",
            "Text/SpritefontGenerator.cs"
        });

        AssertTokenFiles(SharedRoot, "MainRenderer", new[]
        {
            "Rendering/DrawTransaction.cs",
            "Rendering/MainRenderer.cs",
            "Rendering/View.cs"
        });
    }

    [Fact]
    public void MGUI_Core_ForbiddenMonoGameTokenReferences_AreLimitedToKnownPhase4Files()
    {
        AssertTokenFiles(CoreRoot, "GraphicsDevice", new[]
        {
            "UI/Containers/Grids/MGGrid.cs",
            "UI/Containers/Grids/MGUniformGrid.cs",
            "UI/MGDesktop.cs",
            "UI/MGElement.cs"
        });

        AssertTokenFiles(CoreRoot, "SpriteBatch", Array.Empty<string>());
        AssertTokenFiles(CoreRoot, "PrimitiveBatch", Array.Empty<string>());

        AssertTokenFiles(CoreRoot, "Texture2D", new[]
        {
            "UI/Brushes/Border Brushes/MGTexturedBorderBrush.cs",
            "UI/MGImage.cs",
            "UI/MGTextureData.cs"
        });

        AssertTokenFiles(CoreRoot, "RenderTarget2D", Array.Empty<string>());
        AssertTokenFiles(CoreRoot, "ContentManager", Array.Empty<string>());
        AssertTokenFiles(CoreRoot, "MainRenderer", new[] { "UI/MGDesktop.cs" });
    }

    [Fact]
    public void MGUI_Shared_PhysicalConcreteRenderingLeakFiles_AreLimitedToKnownPhase4Allowlist()
    {
        HashSet<string> actual = FindTokenFiles(SharedRoot, ForbiddenMonoGameTokens)
            .Where(path => !SharedContractLeakFiles.Contains(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase)
        {
            "Assets/MonoGameImageResource.cs",
            "Helpers/ContentUtils.cs",
            "Helpers/RenderUtils.cs",
            "Helpers/TextureUtils.cs",
            "Rendering/BackBufferSurface.cs",
            "Rendering/Clipping/ClipManager.cs",
            "Rendering/DelegateRenderHost.cs",
            "Rendering/DrawSettings.cs",
            "Rendering/DrawTransaction.cs",
            "Rendering/MainRenderer.cs",
            "Rendering/RenderTargetPool.cs",
            "Rendering/View.cs",
            "Text/FontManager.cs",
            "Text/FontSet.cs",
            "Text/SpritefontGenerator.cs",
            "Text/Engines/SpriteFontTextEngine.cs"
        };

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    private static void AssertTokenFiles(string projectRoot, string token, string[] expectedRelativePaths)
    {
        HashSet<string> actual = FindTokenFiles(projectRoot, token);
        HashSet<string> expected = expectedRelativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    private static HashSet<string> FindTokenFiles(string projectRoot, params string[] tokens)
    {
        Regex tokenPattern = new($@"\b({string.Join("|", tokens.Select(Regex.Escape))})\b", RegexOptions.CultureInvariant);

        return Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .Where(path => tokenPattern.IsMatch(StripComments(File.ReadAllText(path))))
            .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string StripComments(string source)
    {
        string withoutBlockComments = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlockComments, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }
}