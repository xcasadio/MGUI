using System.IO;
using System.Text.RegularExpressions;

namespace MGUI.Tests.Architecture;

public class RenderingBoundaryArchitectureTests
{
    private static readonly string RepoRoot = @"d:\development\repo\MGUI";
    private static readonly string CoreRoot = Path.Combine(RepoRoot, "MGUI.Core");
    private static readonly string AbstractionsProjectPath = Path.Combine(RepoRoot, "MGUI.Rendering.Abstractions", "MGUI.Rendering.Abstractions.csproj");

    [Fact]
    public void MGUI_Core_DirectMainRendererReferences_AreLimitedToLegacyDesktopEntryPoint()
    {
        AssertTokenFiles(CoreRoot, "MainRenderer", new[]
        {
            "UI/MGDesktop.cs"
        });
    }

    [Fact]
    public void MGUI_Core_DirectDrawTransactionReferences_AreLimitedToKnownRenderingFiles()
    {
        AssertTokenFiles(CoreRoot, "DrawTransaction", new[]
        {
            "UI/MGResources.cs",
            "UI/MGTextureData.cs"
        });
    }

    [Fact]
    public void MGUI_Core_DirectTexture2DReferences_AreLimitedToKnownResourceFiles()
    {
        AssertTokenFiles(CoreRoot, "Texture2D", new[]
        {
            "UI/Brushes/Border Brushes/MGTexturedBorderBrush.cs",
            "UI/MGImage.cs",
            "UI/MGTextureData.cs"
        });
    }

    [Fact]
    public void MGUI_Core_DirectRenderTarget2DReferences_AreLimitedToLegacyWindowScalingCode()
    {
        AssertTokenFiles(CoreRoot, "RenderTarget2D", Array.Empty<string>());
    }

    [Fact]
    public void MGUI_Core_HasNoDirectSpriteBatchOrContentManagerCodeReferences()
    {
        AssertTokenFiles(CoreRoot, "SpriteBatch", Array.Empty<string>());
        AssertTokenFiles(CoreRoot, "ContentManager", Array.Empty<string>());
    }

    [Fact]
    public void MGUI_Core_UsesMeasurementTextContractInsteadOfCompositeTextEngine()
    {
        AssertTokenFiles(CoreRoot, "ITextEngine", Array.Empty<string>());
    }

    [Fact]
    public void RenderingAbstractionsProject_DoesNotReferenceMonoGame_WhenPresent()
    {
        if (!File.Exists(AbstractionsProjectPath))
        {
            return;
        }

        string projectSource = File.ReadAllText(AbstractionsProjectPath);

        Assert.DoesNotContain("MonoGame", projectSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.Xna.Framework", projectSource, StringComparison.OrdinalIgnoreCase);

        string projectDirectory = Path.GetDirectoryName(AbstractionsProjectPath)!;
        foreach (string filePath in Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string source = StripComments(File.ReadAllText(filePath));
            Assert.DoesNotContain("Microsoft.Xna.Framework", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MonoGame", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertTokenFiles(string projectRoot, string token, string[] expectedRelativePaths)
    {
        HashSet<string> actual = FindTokenFiles(projectRoot, token);
        HashSet<string> expected = expectedRelativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
    }

    private static HashSet<string> FindTokenFiles(string projectRoot, string token)
    {
        Regex tokenPattern = new($@"\b{Regex.Escape(token)}\b", RegexOptions.CultureInvariant);

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