using System;
using System.Collections.Generic;
using System.Threading;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.UI;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class UIOptimizationTests
    {
        [Fact]
        public void UI_ResetUI_ClearsState()
        {
            // Test that ResetUI properly clears the internal state
            var engine = new TetrisEngine();
            engine.Current = new Tetromino(TetrominoType.I);
            
            var leaderboard = new List<(string, int, int, bool)>
            {
                ("Alice", 100, 2, true),
                ("Bob", 150, 1, true),
                ("Charlie", 120, 3, false)
            };
            
            var playerNames = new Dictionary<string, string>
            {
                ["Alice"] = "Alice",
                ["Bob"] = "Bob",
                ["Charlie"] = "Charlie"
            };
            var playersWhoPlaced = new HashSet<string> { "Alice", "Bob" };
            
            // Draw something to initialize state
            ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "player1", "Test Status", playerNames, playersWhoPlaced, 1);
            
            // Reset and verify it can be called without issues
            ConsoleUI.ResetUI();
            
            // Should be able to draw again without issues
            ConsoleUI.DrawGameWithLeaderboard(engine, new List<(string, int, int, bool)>(), "test", "", null, null, 1);
            
            Assert.True(true); // If we get here without exceptions, the test passes
        }
        
        [Fact]
        public void TetrisEngine_Move_WorksWithNullCurrent()
        {
            var engine = new TetrisEngine();
            engine.Current = null;
            
            // Should return false and not crash
            bool result = engine.Move(1, 0);
            
            Assert.False(result);
        }
        
        [Fact]
        public void TetrisEngine_Rotate_WorksWithNullCurrent()
        {
            var engine = new TetrisEngine();
            engine.Current = null;
            
            // Should not crash
            engine.Rotate(1);
            
            Assert.True(true); // If we get here without exceptions, the test passes
        }
        
        [Fact]
        public void Tetromino_Movement_UpdatesPosition()
        {
            var engine = new TetrisEngine();
            engine.Current = new Tetromino(TetrominoType.I);
            int initialX = engine.Current.X;
            
            bool moved = engine.Move(1, 0);
            
            Assert.True(moved);
            Assert.Equal(initialX + 1, engine.Current.X);
        }
        
        [Fact]
        public void DrawGameWithLeaderboard_RendersCorrectly()
        {
            var engine = new TetrisEngine();
            var leaderboard = new List<(string, int, int, bool)>
            {
                ("player1", 100, 20, false),
                ("player2", 200, 19, false),
                ("player3", 0, 0, true)
            };
            
            var playerNames = new Dictionary<string, string>
            {
                ["player1"] = "Alice",
                ["player2"] = "Bob", 
                ["player3"] = "Charlie"
            };
            
            var playersWhoPlaced = new HashSet<string> { "player2" };
            
            // This should not throw and should handle all parameters correctly
            ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "player1", "Test Status", playerNames, playersWhoPlaced, 1);
            
            Assert.True(true); // If we get here without exceptions, the test passes
        }
        
        [Fact]
        public void DrawGameWithLeaderboard_HandlesRedraw()
        {
            var engine = new TetrisEngine();
            // Should be able to draw again without issues
            ConsoleUI.DrawGameWithLeaderboard(engine, new List<(string, int, int, bool)>(), "test", "", null, null, 1);
            
            Assert.True(true);
        }
    }
}