using System.IO;

namespace MGUI.Tests.Architecture;

public class TestRepositoryTests
{
    [Fact]
    public void Root_ContainsSolutionAndTestProject()
    {
        Assert.True(File.Exists(Path.Combine(TestRepository.Root, "MGUI.sln")));
        Assert.True(File.Exists(Path.Combine(TestRepository.Root, "MGUI.Tests", "MGUI.Tests.csproj")));
    }

    [Fact]
    public void Root_IsTheCheckoutThatBuiltTheRunningTestAssembly()
    {
        string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        Assert.StartsWith(TestRepository.Root + Path.DirectorySeparatorChar, baseDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
