using System.IO;
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
            "Input\\MonoGameRawInputSource.cs",
            "Rendering\\BackBufferSurface.cs",
            "Rendering\\DelegateRenderHost.cs",
            "Rendering\\DrawTransaction.cs",
            "Rendering\\MainRenderer.cs",
            "Rendering\\RenderTargetPool.cs",
            "Rendering\\View.cs",
            "Rendering\\Clipping\\ClipManager.cs",
            "Text\\Engines\\SpriteFontTextEngine.cs"
        };

        Assert.All(removedFiles, removedFile => Assert.Contains($"<Compile Remove=\"{removedFile}\" />", sharedProjectSource));
    }

    [Fact]
    public void CoreProject_ReferencesExplicitMonoGameBackendProject()
    {
        string coreProjectSource = File.ReadAllText(Path.Combine(RepoRoot, "MGUI.Core", "MGUI.Core.csproj"));

        Assert.Contains("..\\MGUI.MonoGame\\MGUI.MonoGame.csproj", coreProjectSource);
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
}