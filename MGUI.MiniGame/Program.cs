namespace MGUI.MiniGame;

internal static class Program
{
    [System.STAThread]
    private static void Main()
    {
        using MiniGame game = new();
        game.Run();
    }
}
