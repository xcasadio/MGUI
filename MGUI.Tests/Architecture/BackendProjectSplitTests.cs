using System.IO;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class BackendProjectSplitTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void MonoGameSplitProjects_ArePresentInTheSolution()
    {
        string integrationProjectPath = Path.Combine(RepoRoot, "MGUI.MonoGame.Integration", "MGUI.MonoGame.Integration.csproj");
        string legacyRendererProjectPath = Path.Combine(RepoRoot, "MGUI.MonoGame.LegacyRenderer", "MGUI.MonoGame.LegacyRenderer.csproj");
        string solutionSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.sln"));

        Assert.True(File.Exists(integrationProjectPath));
        Assert.True(File.Exists(legacyRendererProjectPath));
        Assert.Contains("MGUI.MonoGame.Integration\\MGUI.MonoGame.Integration.csproj", solutionSource);
        Assert.Contains("MGUI.MonoGame.LegacyRenderer\\MGUI.MonoGame.LegacyRenderer.csproj", solutionSource);
    }

    [Fact]
    public void SharedProject_StopsCompilingConcreteMonoGameBackendFiles()
    {
        string sharedProjectSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.Shared", "MGUI.Shared.csproj"));

        string[] removedFiles =
        {
            "Assets\\MonoGameImageResource.cs",
            "Helpers\\ContentUtils.cs",
            "Helpers\\RenderUtils.cs",
            "Helpers\\TextureUtils.cs",
            "Input\\MonoGameRawInputSource.cs",
            "Rendering\\BackBufferSurface.cs",
            "Rendering\\DelegateRenderHost.cs",
            "Rendering\\DrawTransaction.cs",
            "Rendering\\MainRenderer.cs",
            "Rendering\\RenderTargetPool.cs",
            "Rendering\\View.cs",
            "Rendering\\Clipping\\ClipManager.cs",
            "Text\\FontManager.cs",
            "Text\\FontSet.cs",
            "Text\\SpritefontGenerator.cs",
            "Text\\Engines\\SpriteFontTextEngine.cs"
        };

        Assert.All(removedFiles, removedFile => Assert.Contains($"<Compile Remove=\"{removedFile}\" />", sharedProjectSource));
    }

    [Fact]
    public void IntegrationProject_StripsLegacyRendererFiles()
    {
        string integrationProjectSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.MonoGame.Integration", "MGUI.MonoGame.Integration.csproj"));

        string[] removedFiles =
        {
            "Helpers\\ContentUtils.cs",
            "Helpers\\RenderUtils.cs",
            "Helpers\\TextureUtils.cs",
            "Input\\MonoGameRawInputSource.cs",
            "Rendering\\BackBufferSurface.cs",
            "Rendering\\DelegateRenderHost.cs",
            "Rendering\\DrawTransaction.cs",
            "Rendering\\MainRenderer.cs",
            "Rendering\\RenderTargetPool.cs",
            "Rendering\\View.cs",
            "Rendering\\Clipping\\ClipManager.cs"
        };

        Assert.All(removedFiles, removedFile => Assert.Contains($"<Compile Remove=\"{removedFile}\" />", integrationProjectSource));
        // Les sources Text (FontManager, FontSet, SpritefontGenerator, SpriteFontTextEngine) restent
        // compilees dans l'integration : RendererAssetProvider expose FontManager dans son API publique.
        Assert.DoesNotContain("<Compile Remove=\"Text\\", integrationProjectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference Include=\"MonoGame.Extended\"", integrationProjectSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyRendererProject_LinksConcreteRendererFiles_FromIntegration()
    {
        string legacyRendererProjectSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.MonoGame.LegacyRenderer", "MGUI.MonoGame.LegacyRenderer.csproj"));

        Assert.Contains("..\\MGUI.MonoGame.Integration\\MGUI.MonoGame.Integration.csproj", legacyRendererProjectSource);

        string[] linkedFiles =
        {
            "Properties\\AssemblyInfo.cs",
            "MonoGameBackendBootstrap.cs",
            "Helpers\\ContentUtils.cs",
            "Helpers\\RenderUtils.cs",
            "Helpers\\TextureUtils.cs",
            "Input\\MonoGameRawInputSource.cs",
            "Rendering\\BackBufferSurface.cs",
            "Rendering\\Clipping\\ClipManager.cs",
            "Rendering\\DelegateRenderHost.cs",
            "Rendering\\DrawTransaction.cs",
            "Rendering\\MainRenderer.cs",
            "Rendering\\RenderTargetPool.cs",
            "Rendering\\View.cs"
        };

        Assert.All(linkedFiles, relativePath =>
            Assert.Contains($"<Compile Include=\"..\\MGUI.MonoGame.Integration\\{relativePath}\"", legacyRendererProjectSource));
    }

    [Fact]
    public void CoreAndFontStashSharpProjects_ReferenceMonoGameIntegrationProject()
    {
        string[] projectPaths =
        {
            Path.Combine(RepoRoot, "MGUI.Core", "MGUI.Core.csproj"),
            Path.Combine(RepoRoot, "MGUI.FontStashSharp", "MGUI.FontStashSharp.csproj")
        };

        foreach (string projectPath in projectPaths)
        {
            string projectSource = File.ReadAllText(projectPath);
            Assert.Contains("..\\MGUI.MonoGame.Integration\\MGUI.MonoGame.Integration.csproj", projectSource);
        }
    }

    [Fact]
    public void DemoAndTestProjects_ReferenceLegacyRendererProject()
    {
        string[] projectPaths =
        {
            Path.Combine(RepoRoot, "MGUI.MiniGame", "MGUI.MiniGame.csproj"),
            Path.Combine(RepoRoot, "MGUI.Samples", "MGUI.Samples.csproj"),
            Path.Combine(RepoRoot, "MGUI.Tests", "MGUI.Tests.csproj")
        };

        foreach (string projectPath in projectPaths)
        {
            string projectSource = File.ReadAllText(projectPath);
            Assert.Contains("..\\MGUI.MonoGame.LegacyRenderer\\MGUI.MonoGame.LegacyRenderer.csproj", projectSource);
        }
    }

    [Fact]
    public void RuntimeAndViewContracts_UseSharedDrawTransactionInterface()
    {
        Assert.Equal(typeof(IUIDrawTransaction), typeof(IUIDesktopRuntime)
            .GetMethod(nameof(IUIDesktopRuntime.CreateDrawTransaction))!
            .ReturnType);

        Assert.Equal(typeof(IUIDrawTransaction), typeof(IUIView)
            .GetMethod(nameof(IUIView.Draw))!
            .GetParameters()[0]
            .ParameterType);

        Assert.Equal(typeof(IUIDesktopRuntime), typeof(IUIRenderContext)
            .GetProperty(nameof(IUIRenderContext.Renderer))!
            .PropertyType);
    }

    [Fact]
    public void MonoGameBackendBootstrap_ProvidesExplicitCompositionEntryPoint()
    {
        var createMethod = typeof(MonoGameBackendBootstrap).GetMethod(nameof(MonoGameBackendBootstrap.Create));

        Assert.NotNull(createMethod);
        Assert.True(createMethod!.IsGenericMethodDefinition);
        Assert.Equal(typeof(MonoGameBackendSession<>), createMethod.ReturnType.GetGenericTypeDefinition());

        Type closedSessionType = typeof(MonoGameBackendSession<IRenderHost>);
        Assert.Equal(typeof(IRenderHost), closedSessionType.GetProperty(nameof(MonoGameBackendSession<IRenderHost>.Host))!.PropertyType);
        Assert.Equal(typeof(MainRenderer), closedSessionType.GetProperty(nameof(MonoGameBackendSession<IRenderHost>.Renderer))!.PropertyType);
    }
}