using System;

namespace osuReplayViewer
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (GameBase game = new GameBase())
                game.Run();
        }
    }
}
