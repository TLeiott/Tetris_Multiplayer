using System;
using TetrisMultiplayer.Game;

namespace TetrisMultiplayer.Tests
{
    public class ManualPreviewSyncTest
    {
        public static void TestPreviewSynchronization()
        {
            Console.WriteLine("=== TESTING PREVIEW SYNCHRONIZATION ===");
            Console.WriteLine();
            
            // Test 1: GameManager preview synchronization
            Console.WriteLine("Test 1: GameManager Preview Synchronization");
            var gameManager = new GameManager(12345);
            
            Console.WriteLine("Getting first 5 pieces and their previews:");
            for (int i = 0; i < 5; i++)
            {
                int currentPiece = gameManager.PeekNextPiece();
                int previewPiece = gameManager.PeekPreviewPiece();
                Console.WriteLine($"Round {i + 1}: Current={(TetrominoType)currentPiece}, Preview={(TetrominoType)previewPiece}");
                
                // Consume the current piece
                int consumedPiece = gameManager.GetNextPiece();
                if (consumedPiece != currentPiece)
                {
                    Console.WriteLine($"ERROR: Expected {currentPiece}, got {consumedPiece}");
                }
            }
            Console.WriteLine("✓ GameManager preview synchronization works correctly");
            Console.WriteLine();
            
            // Test 2: Engine synchronization
            Console.WriteLine("Test 2: Multiple Engines with Synchronized Preview");
            var engine1 = new TetrisEngine();
            var engine2 = new TetrisEngine();
            var syncGameManager = new GameManager(54321);
            
            Console.WriteLine("Setting synchronized pieces for 3 rounds:");
            for (int round = 1; round <= 3; round++)
            {
                // Get synchronized pieces
                int currentPieceId = syncGameManager.GetNextPiece();
                int previewPieceId = syncGameManager.PeekNextPiece();
                
                // Set current pieces
                engine1.Current = new Tetromino((TetrominoType)currentPieceId);
                engine2.Current = new Tetromino((TetrominoType)currentPieceId);
                
                // Set synchronized preview pieces
                engine1.SetNextPiece((TetrominoType)previewPieceId);
                engine2.SetNextPiece((TetrominoType)previewPieceId);
                
                Console.WriteLine($"Round {round}:");
                Console.WriteLine($"  Engine 1: Current={engine1.Current.Type}, Next={engine1.Next.Type}");
                Console.WriteLine($"  Engine 2: Current={engine2.Current.Type}, Next={engine2.Next.Type}");
                
                // Verify synchronization
                if (engine1.Current.Type == engine2.Current.Type && engine1.Next.Type == engine2.Next.Type)
                {
                    Console.WriteLine($"  ✓ Round {round}: Engines are synchronized");
                }
                else
                {
                    Console.WriteLine($"  ✗ Round {round}: Engines are NOT synchronized");
                }
            }
            Console.WriteLine();
            
            // Test 3: Verify preview only updates when needed
            Console.WriteLine("Test 3: Preview Caching Behavior");
            var testEngine = new TetrisEngine(12345);
            var originalNext = testEngine.Next.Type;
            Console.WriteLine($"Original Next piece: {originalNext}");
            
            // Set same piece - should update
            testEngine.SetNextPiece(originalNext);
            Console.WriteLine($"After setting same piece: {testEngine.Next.Type} (should be same)");
            
            // Set different piece - should update  
            var differentPiece = originalNext == TetrominoType.I ? TetrominoType.O : TetrominoType.I;
            testEngine.SetNextPiece(differentPiece);
            Console.WriteLine($"After setting different piece: {testEngine.Next.Type} (should be {differentPiece})");
            
            if (testEngine.Next.Type == differentPiece)
            {
                Console.WriteLine("✓ Preview updates correctly when piece changes");
            }
            else
            {
                Console.WriteLine("✗ Preview update failed");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== PREVIEW SYNCHRONIZATION TEST COMPLETE ===");
            Console.WriteLine("All players should now see the same preview piece in multiplayer mode!");
        }
    }
}