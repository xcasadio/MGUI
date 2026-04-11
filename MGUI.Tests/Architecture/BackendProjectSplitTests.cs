using System.IO;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class BackendProjectSplitTests
{
    private static readonly string RepoRoot = @"d:\development\repo\MGUI";

    [Fact]
    public void MonoGameBackendProject_IsPresentInTheSolution()
    {
        string projectPath = Path.Combine(RepoRoot, "MGUI.MonoGame", "MGUI.MonoGame.csproj");
        string solutionSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.sln"));

        Assert.True(File.Exists(projectPath));
        Assert.Contains("MGUI.MonoGame\\MGUI.MonoGame.csproj", solutionSource);
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
    public void MonoGameProject_OwnsConcreteBackendFilesLocally_WithoutLinkedIncludesFromShared()
    {
        string monoGameProjectSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.MonoGame", "MGUI.MonoGame.csproj"));

        Assert.DoesNotContain("<Compile Include=\"..\\MGUI.Shared\\", monoGameProjectSource, StringComparison.OrdinalIgnoreCase);

        string[] expectedLocalFiles =
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

        Assert.All(expectedLocalFiles, relativePath =>
            Assert.True(File.Exists(Path.Combine(RepoRoot, "MGUI.MonoGame", relativePath)), $"Expected local backend source '{relativePath}' to exist under MGUI.MonoGame."));
    }

    [Fact]
    public void CoreProject_ReferencesExplicitMonoGameBackendProject()
    {
        string coreProjectSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.Core", "MGUI.Core.csproj"));

        Assert.Contains("..\\MGUI.MonoGame\\MGUI.MonoGame.csproj", coreProjectSource);
    }

    [Fact]
    public void DemoProjects_ReferenceExplicitMonoGameBackendProject()
    {
        string[] projectPaths =
        {
            Path.Combine(RepoRoot, "MGUI.MiniGame", "MGUI.MiniGame.csproj"),
            Path.Combine(RepoRoot, "MGUI.Samples", "MGUI.Samples.csproj")
        };

        foreach (string projectPath in projectPaths)
        {
            string projectSource = File.ReadAllText(projectPath);
            Assert.Contains("..\\MGUI.MonoGame\\MGUI.MonoGame.csproj", projectSource);
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