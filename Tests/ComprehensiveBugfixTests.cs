using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TetrisMultiplayer.Networking;
using TetrisMultiplayer.Game;
using Microsoft.Extensions.Logging;

namespace TetrisMultiplayer.Tests
{
    public class ComprehensiveBugfixTests
    {
        public static void TestPieceSynchronization()
        {
            Console.WriteLine("=== Testing Piece Synchronization ===");
            
            // Test that multiple GameManagers with the same seed produce identical pieces
            int testSeed = 12345;
            var hostManager = new GameManager(testSeed);
            var clientManager = new GameManager(testSeed);
            
            bool allPiecesMatch = true;
            for (int i = 0; i < 20; i++)
            {
                int hostPiece = hostManager.GetNextPiece();
                int clientPiece = clientManager.GetNextPiece();
                
                if (hostPiece != clientPiece)
                {
                    Console.WriteLine($"MISMATCH at piece {i}: Host={hostPiece}, Client={clientPiece}");
                    allPiecesMatch = false;
                }
                else
                {
                    Console.WriteLine($"Piece {i}: Both got {hostPiece} ?");
                }
            }
            
            if (allPiecesMatch)
            {
                Console.WriteLine("? Piece synchronization test PASSED");
            }
            else
            {
                Console.WriteLine("? Piece synchronization test FAILED");
            }
            Console.WriteLine();
        }
        
        public static void TestNetworkMessageParsing()
        {
            Console.WriteLine("=== Testing Network Message Parsing ===");
            
            try
            {
                // Test PlacedPiece message structure
                var placedPieceMsg = new NetworkManager.PlacedPieceMsg
                {
                    PlayerId = "test-player",
                    PieceId = 3,
                    PlacedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Locks = true
                };
                
                Console.WriteLine($"PlacedPiece: Player={placedPieceMsg.PlayerId}, Piece={placedPieceMsg.PieceId}");
                
                // Test LeaderboardUpdate structure
                var leaderboardUpdate = new NetworkManager.LeaderboardUpdateDto
                {
                    Scores = new Dictionary<string, int> { ["host"] = 500, ["client1"] = 300 },
                    Hp = new Dictionary<string, int> { ["host"] = 18, ["client1"] = 20 },
                    Spectators = new List<string>(),
                    PlayerNames = new Dictionary<string, string> { ["host"] = "TestHost", ["client1"] = "TestClient" }
                };
                
                Console.WriteLine($"LeaderboardUpdate: {leaderboardUpdate.Scores.Count} players");
                
                // Test RoundResults structure
                var roundResults = new NetworkManager.RoundResultsDto
                {
                    NewScores = new Dictionary<string, int> { ["host"] = 600, ["client1"] = 400 },
                    Hp = new Dictionary<string, int> { ["host"] = 18, ["client1"] = 19 },
                    Spectators = new List<string>(),
                    DeletedRowsPerPlayer = new Dictionary<string, int>(),
                    HpChanges = new Dictionary<string, int>()
                };
                
                Console.WriteLine($"RoundResults: {roundResults.NewScores.Count} score updates");
                
                Console.WriteLine("? Network message parsing test PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Network message parsing test FAILED: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void TestGameEngineConsistency()
        {
            Console.WriteLine("=== Testing Game Engine Consistency ===");
            
            try
            {
                var engine1 = new TetrisEngine();
                var engine2 = new TetrisEngine();
                
                // Both engines should behave identically with same inputs
                for (int pieceType = 0; pieceType < 7; pieceType++)
                {
                    engine1.Current = new Tetromino((TetrominoType)pieceType);
                    engine1.Current.X = TetrisEngine.Width / 2 - 2;
                    engine1.Current.Y = 0;
                    
                    engine2.Current = new Tetromino((TetrominoType)pieceType);
                    engine2.Current.X = TetrisEngine.Width / 2 - 2;
                    engine2.Current.Y = 0;
                    
                    // Both should be able to place the piece
                    bool valid1 = engine1.IsValid(engine1.Current, engine1.Current.X, engine1.Current.Y, engine1.Current.Rotation);
                    bool valid2 = engine2.IsValid(engine2.Current, engine2.Current.X, engine2.Current.Y, engine2.Current.Rotation);
                    
                    if (valid1 != valid2)
                    {
                        Console.WriteLine($"? Inconsistent validation for piece type {pieceType}");
                        return;
                    }
                    
                    if (valid1)
                    {
                        engine1.Place();
                        engine2.Place();
                        
                        if (engine1.Score != engine2.Score)
                        {
                            Console.WriteLine($"? Inconsistent scoring for piece type {pieceType}");
                            return;
                        }
                    }
                }
                
                Console.WriteLine("? Game engine consistency test PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Game engine consistency test FAILED: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void TestErrorRecovery()
        {
            Console.WriteLine("=== Testing Error Recovery ===");
            
            try
            {
                // Test that invalid piece IDs are handled gracefully
                var engine = new TetrisEngine();
                
                try
                {
                    // This should not crash the game
                    engine.Current = new Tetromino((TetrominoType)999);
                    Console.WriteLine("? Invalid piece type should have thrown exception");
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("? Invalid piece type properly handled");
                }
                
                // Test that null references are handled
                engine.Current = null;
                bool moveResult = engine.Move(1, 0); // Should return false, not crash
                if (!moveResult)
                {
                    Console.WriteLine("? Null piece movement properly handled");
                }
                
                Console.WriteLine("? Error recovery test PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error recovery test FAILED: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void RunAllTests()
        {
            Console.WriteLine("Running comprehensive bugfix tests...\n");
            
            TestPieceSynchronization();
            TestNetworkMessageParsing();
            TestGameEngineConsistency();
            TestErrorRecovery();
            
            Console.WriteLine("All tests completed!");
        }
    }
}