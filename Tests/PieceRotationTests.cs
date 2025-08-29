using System;
using System.Linq;
using TetrisMultiplayer.Game;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class PieceRotationTests
    {
        [Theory]
        [InlineData(TetrominoType.I)]
        [InlineData(TetrominoType.O)]
        [InlineData(TetrominoType.T)]
        [InlineData(TetrominoType.S)]
        [InlineData(TetrominoType.Z)]
        [InlineData(TetrominoType.J)]
        [InlineData(TetrominoType.L)]
        public void Tetromino_Rotates_And_Blocks_Are_Valid(TetrominoType type)
        {
            var t = new Tetromino(type);
            var blocks0 = t.Blocks().ToArray();
            t.Rotation = 1;
            var blocks1 = t.Blocks().ToArray();
            t.Rotation = 2;
            var blocks2 = t.Blocks().ToArray();
            t.Rotation = 3;
            var blocks3 = t.Blocks().ToArray();
            Assert.Equal(4, blocks0.Length);
            Assert.Equal(4, blocks1.Length);
            Assert.Equal(4, blocks2.Length);
            Assert.Equal(4, blocks3.Length);
            // All blocks should be within field bounds after spawn
            foreach (var arr in new[] { blocks0, blocks1, blocks2, blocks3 })
                foreach (var (x, y) in arr)
                    Assert.InRange(x, 0, TetrisEngine.Width);
        }
    }
}
