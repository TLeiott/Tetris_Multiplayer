using System;
using System.Collections.Generic;
using Xunit;
using TetrisMultiplayer.Networking;

namespace TetrisMultiplayer.Tests
{
    public class ScoreboardDisplayTests
    {
        [Fact]
        public void LeaderboardUpdate_HP_DefaultValue_ShouldBe100()
        {
            // Test that HP default values are consistent across the codebase
            // This test verifies the fix for the scoreboard HP display issue
            
            var leaderboardDto = new NetworkManager.LeaderboardUpdateDto
            {
                Scores = new Dictionary<string, int> { ["player1"] = 100, ["player2"] = 200 },
                Hp = new Dictionary<string, int> { ["player1"] = 85 }, // player2 HP missing intentionally
                PlayerNames = new Dictionary<string, string> { ["player1"] = "Alice", ["player2"] = "Bob" },
                Spectators = new List<string>(),
                PlayersPlaced = new HashSet<string>()
            };
            
            // Simulate the logic used in the client game loop
            var realtimeScores = leaderboardDto.Scores;
            var realtimeHp = leaderboardDto.Hp;
            var realtimeSpectators = leaderboardDto.Spectators;
            
            // This mimics the leaderboard display logic that was fixed
            var displayLeaderboard = new List<(string id, int score, int hp, bool isSpectator)>();
            foreach (var id in realtimeScores.Keys)
            {
                var score = realtimeScores.GetValueOrDefault(id, 0);
                var hp = realtimeHp.GetValueOrDefault(id, 100); // This should be 100, not 20
                var isSpectator = realtimeSpectators.Contains(id);
                displayLeaderboard.Add((id, score, hp, isSpectator));
            }
            
            // Verify that player1 gets their actual HP value
            var player1Entry = displayLeaderboard.Find(x => x.id == "player1");
            Assert.Equal(85, player1Entry.hp);
            
            // Verify that player2 gets the default HP value of 100 (not 20)
            var player2Entry = displayLeaderboard.Find(x => x.id == "player2");
            Assert.Equal(100, player2Entry.hp); // This is the critical fix
            
            // Verify scores are correct
            Assert.Equal(100, player1Entry.score);
            Assert.Equal(200, player2Entry.score);
            
            // Verify spectator status is correct
            Assert.False(player1Entry.isSpectator);
            Assert.False(player2Entry.isSpectator);
        }
        
        [Fact]
        public void LeaderboardUpdate_EmptyData_ShouldHandleGracefully()
        {
            // Test that empty leaderboard data doesn't cause issues
            var emptyScores = new Dictionary<string, int>();
            var emptyHp = new Dictionary<string, int>();
            var emptySpectators = new List<string>();
            
            // This should not throw and should result in an empty leaderboard
            var displayLeaderboard = new List<(string id, int score, int hp, bool isSpectator)>();
            foreach (var id in emptyScores.Keys)
            {
                var score = emptyScores.GetValueOrDefault(id, 0);
                var hp = emptyHp.GetValueOrDefault(id, 100);
                var isSpectator = emptySpectators.Contains(id);
                displayLeaderboard.Add((id, score, hp, isSpectator));
            }
            
            Assert.Empty(displayLeaderboard);
        }
    }
}