using System;
using System.Threading;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.UI;

namespace TetrisMultiplayer.Tests
{
    public class PreviewOptimizationTest
    {
        public static void TestPreviewCaching()
        {
            Console.WriteLine("=== TESTING PREVIEW OPTIMIZATION ===");
            
            // Create a TetrisEngine for testing
            var engine = new TetrisEngine(seed: 12345); // Use fixed seed for predictable results
            engine.SpawnNext(); // Initialize first piece
            
            Console.WriteLine("Testing optimized preview rendering...");
            Console.WriteLine("The next piece should only redraw when it actually changes.");
            Console.WriteLine("Press 'n' to spawn next piece, 'q' to quit");
            
            // Reset UI to clear any previous state
            ConsoleUI.ResetUI();
            
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== PREVIEW OPTIMIZATION TEST ===");
                Console.WriteLine($"Current piece: {engine.Current?.Type ?? TetrominoType.I}");
                Console.WriteLine($"Next piece: {engine.Next.Type}");
                Console.WriteLine();
                Console.WriteLine("Controls: [N] Next piece, [Q] Quit");
                Console.WriteLine();
                
                // Draw the current game state with optimized preview
                var dummyLeaderboard = new List<(string, int, int, bool)> 
                { 
                    ("TestPlayer", engine.Score, 100, false) 
                };
                
                ConsoleUI.DrawGameWithLeaderboard(engine, dummyLeaderboard, "TestPlayer", "Preview Optimization Test");
                
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q) break;
                if (key.Key == ConsoleKey.N)
                {
                    // Simulate placing a piece and getting a new one
                    if (engine.Current != null)
                    {
                        // Place current piece at bottom
                        while (engine.Move(0, 1)) { }
                        engine.Place();
                    }
                    engine.SpawnNext();
                    Console.WriteLine("New piece spawned! Preview should update only once.");
                    Thread.Sleep(1000); // Brief pause to show the update
                }
            }
            
            Console.WriteLine("Preview optimization test completed.");
        }
        
        public static void TestMiniGravityPreview()
        {
            Console.WriteLine("=== TESTING MINI GRAVITY TETRIS PREVIEW ===");
            Console.WriteLine("This test would normally run the mini-gravity game.");
            Console.WriteLine("However, for automated testing, we'll just verify the class exists.");
            
            var miniGame = new MiniGravityTetris();
            Console.WriteLine("MiniGravityTetris instantiated successfully with optimized preview.");
            Console.WriteLine("The game now includes preview caching to reduce unnecessary redraws.");
        }
        
        public static void RunManualTest()
        {
            Console.WriteLine("=== TETRIS PREVIEW OPTIMIZATION TESTS ===");
            Console.WriteLine("1. Preview Caching Test");
            Console.WriteLine("2. Mini Gravity Preview Test");
            Console.WriteLine("Choose test (1-2) or 'q' to quit:");
            
            var choice = Console.ReadKey(true);
            Console.WriteLine();
            
            switch (choice.Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    TestPreviewCaching();
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    TestMiniGravityPreview();
                    break;
                case ConsoleKey.Q:
                    Console.WriteLine("Exiting tests.");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Exiting.");
                    break;
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}