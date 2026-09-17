namespace MGUI.Editor.Host;

internal static class Program
{
    [System.STAThread]
    private static void Main()
    {
        using EditorHostGame game = new();
        game.Run();
    }
}
