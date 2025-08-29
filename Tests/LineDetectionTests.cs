using System;
using TetrisMultiplayer.Game;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class LineDetectionTests
    {
        [Fact]
        public void Detects_And_Clears_Full_Lines()
        {
            var engine = new TetrisEngine();
            // Fülle unterste Zeile
            for (int x = 0; x < TetrisEngine.Width; x++)
                engine.Grid[TetrisEngine.Height - 1, x] = 1;
            int lines = engine.ClearLines();
            Assert.Equal(1, lines);
            for (int x = 0; x < TetrisEngine.Width; x++)
                Assert.Equal(0, engine.Grid[TetrisEngine.Height - 1, x]);
        }
    }
}
