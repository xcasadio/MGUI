using System.IO;

namespace MGUI.Tests;

/// <summary>
/// Resolves the repository root from the location of the running test assembly, instead of a
/// hard-coded absolute path. Tests must read the sources of the checkout (or git worktree) they
/// were actually built from, not an arbitrary checkout that happens to live at a fixed path on
/// the machine.
/// </summary>
internal static class TestRepository
{
    public static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static string Combine(params string[] relativeSegments) => Path.Combine(Root, Path.Combine(relativeSegments));
}
