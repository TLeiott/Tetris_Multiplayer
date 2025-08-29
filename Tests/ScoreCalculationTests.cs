using System;
using TetrisMultiplayer.Game;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class ScoreCalculationTests
    {
        [Theory]
        [InlineData(1, 100)]
        [InlineData(2, 300)]
        [InlineData(3, 500)]
        [InlineData(4, 800)]
        [InlineData(0, 0)]
        public void Score_Is_Correct_For_Lines(int lines, int expectedScore)
        {
            int score = lines switch { 1 => 100, 2 => 300, 3 => 500, 4 => 800, _ => 0 };
            Assert.Equal(expectedScore, score);
        }
    }
}
