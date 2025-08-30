using System;
using System.Collections.Generic;
using System.Linq;

namespace TetrisMultiplayer.Tests
{
    /// <summary>
    /// Simple demonstration program that shows the before/after behavior of the leaderboard fix
    /// </summary>
    public class LeaderboardFixDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("=== Tetris Multiplayer Leaderboard Fix Demo ===");
            Console.WriteLine();
            
            // Simulate the scenario before the fix
            Console.WriteLine("BEFORE FIX:");
            Console.WriteLine("Client starts with empty real-time leaderboard data...");
            SimulateClientBehaviorBeforeFix();
            
            Console.WriteLine();
            Console.WriteLine("AFTER FIX:");
            Console.WriteLine("Client receives initial leaderboard data when game starts...");
            SimulateClientBehaviorAfterFix();
            
            Console.WriteLine();
            Console.WriteLine("=== Demo Complete ===");
        }
        
        private static void SimulateClientBehaviorBeforeFix()
        {
            // Before fix: Client starts with empty leaderboard data
            var realtimeScores = new Dictionary<string, int>(); // Empty!
            var realtimeHp = new Dictionary<string, int>(); // Empty!
            var realtimeSpectators = new List<string>();
            var playerId = "client1";
            var engineScore = 0;
            
            // Client tries to create leaderboard (this is the logic from line 1518-1526)
            var displayLeaderboard = realtimeScores.Count > 0 
                ? realtimeScores.Keys.Select(id => 
                {
                    var score = realtimeScores.GetValueOrDefault(id, 0);
                    if (id == playerId)
                        score = Math.Max(score, engineScore);
                    return (id, score, realtimeHp.GetValueOrDefault(id, 100), realtimeSpectators.Contains(id));
                }).ToList()
                : new List<(string, int, int, bool)>{ (playerId, engineScore, 100, false) }; // Fallback: only shows self!
            
            Console.WriteLine($"  Leaderboard shows: {displayLeaderboard.Count} player(s)");
            foreach (var (id, score, hp, isSpectator) in displayLeaderboard)
            {
                Console.WriteLine($"    {id}: Score={score}, HP={hp}, Spectator={isSpectator}");
            }
            Console.WriteLine("  ❌ PROBLEM: Only shows the local player!");
        }
        
        private static void SimulateClientBehaviorAfterFix()
        {
            // After fix: Client receives initial leaderboard data from host
            var realtimeScores = new Dictionary<string, int> 
            { 
                ["host"] = 0, 
                ["client1"] = 0, 
                ["client2"] = 0 
            }; // Now populated!
            
            var realtimeHp = new Dictionary<string, int> 
            { 
                ["host"] = 100, 
                ["client1"] = 100, 
                ["client2"] = 100 
            }; // Now populated!
            
            var realtimeSpectators = new List<string>();
            var playerId = "client1";
            var engineScore = 0;
            
            // Client creates leaderboard with initial data
            var displayLeaderboard = realtimeScores.Count > 0 
                ? realtimeScores.Keys.Select(id => 
                {
                    var score = realtimeScores.GetValueOrDefault(id, 0);
                    if (id == playerId)
                        score = Math.Max(score, engineScore);
                    return (id, score, realtimeHp.GetValueOrDefault(id, 100), realtimeSpectators.Contains(id));
                }).ToList()
                : new List<(string, int, int, bool)>{ (playerId, engineScore, 100, false) };
            
            Console.WriteLine($"  Leaderboard shows: {displayLeaderboard.Count} player(s)");
            foreach (var (id, score, hp, isSpectator) in displayLeaderboard)
            {
                Console.WriteLine($"    {id}: Score={score}, HP={hp}, Spectator={isSpectator}");
            }
            Console.WriteLine("  ✅ FIXED: Shows all players from the start!");
        }
    }
}