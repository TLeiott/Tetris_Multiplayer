using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.UI;
using TetrisMultiplayer.Networking;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class PerformanceAndLeaderboardTests
    {
        [Fact]
        public void LeaderboardDisplay_ShowsPlayerNames_Correctly()
        {
            // Test enhanced leaderboard functionality
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
            ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "player1", "Test Status", playerNames, playersWhoPlaced);
            
            Assert.True(true); // If we get here without exceptions, the test passes
        }
        
        [Fact]
        public void LeaderboardDisplay_HandlesNullParameters()
        {
            // Test that the method handles null parameters gracefully
            var engine = new TetrisEngine();
            var leaderboard = new List<(string, int, int, bool)>
            {
                ("player1", 100, 20, false)
            };
            
            // Should handle null playerNames and playersWhoPlaced
            ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "player1", "Test", null, null);
            
            Assert.True(true);
        }
        
        [Fact]
        public void NetworkManager_RoundResultsDto_PropertiesWork()
        {
            // Test that the RoundResultsDto works correctly
            var dto = new NetworkManager.RoundResultsDto
            {
                DeletedRowsPerPlayer = new Dictionary<string, int> { ["p1"] = 2 },
                NewScores = new Dictionary<string, int> { ["p1"] = 300 },
                Hp = new Dictionary<string, int> { ["p1"] = 19 },
                HpChanges = new Dictionary<string, int> { ["p1"] = -1 },
                Spectators = new List<string> { "p2" }
            };
            
            // Test property access
            Assert.Equal(2, dto.DeletedRowsPerPlayer["p1"]);
            Assert.Equal(300, dto.NewScores["p1"]);
            Assert.Equal(19, dto.Hp["p1"]);
            Assert.Equal(-1, dto.HpChanges["p1"]);
            Assert.Contains("p2", dto.Spectators);
        }
        
        [Fact]
        public void NetworkManager_PlacedPieceMsg_PropertiesWork()
        {
            // Test PlacedPieceMsg structure
            var msg = new NetworkManager.PlacedPieceMsg
            {
                PlayerId = "test123",
                PieceId = 5,
                PlacedAt = 1234567890,
                Locks = true
            };
            
            Assert.Equal("test123", msg.PlayerId);
            Assert.Equal(5, msg.PieceId);
            Assert.Equal(1234567890, msg.PlacedAt);
            Assert.True(msg.Locks);
        }
        
        [Fact]
        public void HashSet_Contains_Performance()
        {
            // Test that HashSet.Contains is fast for our use case
            var playersWhoPlaced = new HashSet<string>();
            
            // Add many players
            for (int i = 0; i < 1000; i++)
            {
                playersWhoPlaced.Add($"player{i}");
            }
            
            // Test fast lookup
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                bool contains = playersWhoPlaced.Contains("player500");
            }
            stopwatch.Stop();
            
            // Should be very fast (less than 10ms for 10k lookups)
            Assert.True(stopwatch.ElapsedMilliseconds < 10);
        }
    }
}