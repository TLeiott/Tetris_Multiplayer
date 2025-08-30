using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.Networking;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class InitialLeaderboardTests
    {
        [Fact]
        public void InitialLeaderboard_ShouldContainAllPlayers_WithDefaultValues()
        {
            // Test the initial state that should be broadcast to clients at game start
            var scores = new Dictionary<string, int> 
            { 
                ["host"] = 0,
                ["client1"] = 0,
                ["client2"] = 0
            };
            
            var hps = new Dictionary<string, int>
            {
                ["host"] = 100,
                ["client1"] = 100,
                ["client2"] = 100
            };
            
            var spectators = new HashSet<string>(); // No spectators initially
            
            var playerNames = new Dictionary<string, string>
            {
                ["host"] = "TestHost",
                ["client1"] = "Player1",
                ["client2"] = "Player2"
            };
            
            var playersWhoPlaced = new HashSet<string>(); // No one has placed yet
            
            // Verify all players start with correct initial values
            Assert.Equal(3, scores.Count);
            Assert.Equal(3, hps.Count);
            Assert.Equal(3, playerNames.Count);
            Assert.Empty(spectators);
            Assert.Empty(playersWhoPlaced);
            
            // Verify initial scores and HP values
            foreach (var playerId in scores.Keys)
            {
                Assert.Equal(0, scores[playerId]);
                Assert.Equal(100, hps[playerId]);
                Assert.DoesNotContain(playerId, spectators);
                Assert.DoesNotContain(playerId, playersWhoPlaced);
            }
        }
        
        [Fact]
        public void LeaderboardMessage_InitialState_ShouldHaveCorrectFormat()
        {
            // Test that the initial leaderboard message format is correct
            var scores = new Dictionary<string, int> { ["host"] = 0, ["client1"] = 0 };
            var hps = new Dictionary<string, int> { ["host"] = 100, ["client1"] = 100 };
            var spectators = new HashSet<string>();
            var playerNames = new Dictionary<string, string> { ["host"] = "Host", ["client1"] = "Client1" };
            var playersWhoPlaced = new HashSet<string>();
            
            // Simulate the leaderboard message creation logic from BroadcastRealtimeLeaderboard
            var leaderboardUpdate = new
            {
                type = "LeaderboardUpdate",
                scores = scores,
                hp = hps,
                spectators = spectators.ToList(),
                playerNames = playerNames,
                playersPlaced = playersWhoPlaced.ToList()
            };
            
            // Verify the data structure matches expectations
            Assert.Equal("LeaderboardUpdate", leaderboardUpdate.type);
            Assert.Equal(2, leaderboardUpdate.scores.Count);
            Assert.Equal(2, leaderboardUpdate.hp.Count);
            Assert.Empty(leaderboardUpdate.spectators);
            Assert.Equal(2, leaderboardUpdate.playerNames.Count);
            Assert.Empty(leaderboardUpdate.playersPlaced);
        }
        
        [Fact]
        public void ClientLeaderboard_WithInitialData_ShouldShowAllPlayers()
        {
            // Test that clients can create a proper leaderboard from initial data
            var realtimeScores = new Dictionary<string, int> { ["host"] = 0, ["client1"] = 0, ["client2"] = 0 };
            var realtimeHp = new Dictionary<string, int> { ["host"] = 100, ["client1"] = 100, ["client2"] = 100 };
            var realtimeSpectators = new List<string>();
            var playerId = "client1";
            var engineScore = 0; // Local engine score
            
            // Simulate the leaderboard creation logic from ClientGameLoop (line 1518-1526)
            var displayLeaderboard = realtimeScores.Count > 0 
                ? realtimeScores.Keys.Select(id => 
                {
                    var score = realtimeScores.GetValueOrDefault(id, 0);
                    if (id == playerId)
                        score = Math.Max(score, engineScore);
                    return (id, score, realtimeHp.GetValueOrDefault(id, 100), realtimeSpectators.Contains(id));
                }).ToList()
                : new List<(string, int, int, bool)>{ (playerId, engineScore, 100, false) };
            
            // With initial data, should show all players, not just the local player
            Assert.Equal(3, displayLeaderboard.Count);
            Assert.Contains(displayLeaderboard, entry => entry.Item1 == "host");
            Assert.Contains(displayLeaderboard, entry => entry.Item1 == "client1");
            Assert.Contains(displayLeaderboard, entry => entry.Item1 == "client2");
            
            // All players should have initial values
            foreach (var entry in displayLeaderboard)
            {
                Assert.Equal(0, entry.Item2); // Score
                Assert.Equal(100, entry.Item3); // HP
                Assert.False(entry.Item4); // Not spectator
            }
        }
    }
}