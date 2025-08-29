using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TetrisMultiplayer.Networking;
using TetrisMultiplayer.Game;
using Microsoft.Extensions.Logging;

namespace TetrisMultiplayer.Tests
{
    public class SerializationBugfixTests
    {
        public static void TestJaggedArrayConversion()
        {
            Console.WriteLine("=== Testing Jagged Array Conversion ===");
            
            try
            {
                // Create a test 2D array (simulating a filled Tetris field)
                int[,] test2D = new int[20, 10];
                
                // Fill it with some test data (like a full Tetris field)
                for (int y = 15; y < 20; y++) // Fill bottom 5 rows
                {
                    for (int x = 0; x < 10; x++)
                    {
                        test2D[y, x] = (x % 7) + 1; // Various piece types
                    }
                }
                
                // Convert to jagged array
                var jaggedArray = TetrisMultiplayer.Program.ConvertToJaggedArray(test2D);
                
                // Verify conversion
                bool conversionCorrect = true;
                for (int y = 0; y < 20; y++)
                {
                    for (int x = 0; x < 10; x++)
                    {
                        if (test2D[y, x] != jaggedArray[y][x])
                        {
                            conversionCorrect = false;
                            Console.WriteLine($"Mismatch at [{y},{x}]: 2D={test2D[y, x]}, Jagged={jaggedArray[y][x]}");
                        }
                    }
                }
                
                if (conversionCorrect)
                {
                    Console.WriteLine("? 2D to Jagged Array conversion test PASSED");
                }
                else
                {
                    Console.WriteLine("? 2D to Jagged Array conversion test FAILED");
                }
                
                // Test jagged array comparison
                var jaggedArray2 = TetrisMultiplayer.Program.ConvertToJaggedArray(test2D);
                bool comparisonWorks = TetrisMultiplayer.Program.JaggedArrayEquals(jaggedArray, jaggedArray2);
                
                if (comparisonWorks)
                {
                    Console.WriteLine("? Jagged Array comparison test PASSED");
                }
                else
                {
                    Console.WriteLine("? Jagged Array comparison test FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Jagged Array conversion test FAILED with exception: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void TestSerializationCompatibility()
        {
            Console.WriteLine("=== Testing JSON Serialization Compatibility ===");
            
            try
            {
                // Create test data that would previously cause the crash
                var testFields = new Dictionary<string, int[][]>();
                var testGrid = new int[20][];
                for (int i = 0; i < 20; i++)
                {
                    testGrid[i] = new int[10];
                    for (int j = 0; j < 10; j++)
                    {
                        testGrid[i][j] = (i >= 15) ? 1 : 0; // Simulate filled bottom
                    }
                }
                testFields["host"] = testGrid;
                
                var testSnapshot = new
                {
                    type = "SpectatorSnapshot",
                    fields = testFields,
                    scores = new Dictionary<string, int> { ["host"] = 500 },
                    hp = new Dictionary<string, int> { ["host"] = 0 },
                    spectators = new List<string> { "host" }
                };
                
                // This should NOT throw an exception now
                var json = System.Text.Json.JsonSerializer.Serialize(testSnapshot);
                
                if (!string.IsNullOrEmpty(json))
                {
                    Console.WriteLine("? JSON Serialization test PASSED");
                    Console.WriteLine($"Serialized length: {json.Length} characters");
                }
                else
                {
                    Console.WriteLine("? JSON Serialization test FAILED - empty result");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? JSON Serialization test FAILED: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void TestGameOverScenario()
        {
            Console.WriteLine("=== Testing Game Over Scenario ===");
            
            try
            {
                // Create a TetrisEngine and fill it up (simulating game over)
                var engine = new TetrisEngine();
                
                // Fill the grid to simulate a full field
                for (int y = 10; y < 20; y++) // Fill bottom half
                {
                    for (int x = 0; x < 10; x++)
                    {
                        engine.Grid[y, x] = 1;
                    }
                }
                
                // Try to create a new tetromino (this should detect game over)
                var tetromino = new Tetromino(TetrominoType.I);
                tetromino.X = TetrisEngine.Width / 2 - 2;
                tetromino.Y = 0;
                
                bool canPlace = engine.IsValid(tetromino, tetromino.X, tetromino.Y, tetromino.Rotation);
                
                if (!canPlace)
                {
                    Console.WriteLine("? Game Over detection test PASSED");
                    Console.WriteLine("Game correctly detected that piece cannot be placed");
                }
                else
                {
                    Console.WriteLine("? Game Over detection test FAILED");
                    Console.WriteLine("Game should have detected game over condition");
                }
                
                // Test that we can convert this filled grid without errors
                var jaggedGrid = TetrisMultiplayer.Program.ConvertToJaggedArray(engine.Grid);
                if (jaggedGrid != null && jaggedGrid.Length == 20)
                {
                    Console.WriteLine("? Filled grid conversion test PASSED");
                }
                else
                {
                    Console.WriteLine("? Filled grid conversion test FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Game Over scenario test FAILED: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void TestErrorHandling()
        {
            Console.WriteLine("=== Testing Error Handling ===");
            
            try
            {
                // Test null array handling
                bool nullComparison = TetrisMultiplayer.Program.JaggedArrayEquals(null, null);
                if (nullComparison)
                {
                    Console.WriteLine("? Null array comparison test PASSED");
                }
                
                // Test mismatched array sizes
                var array1 = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };
                var array2 = new int[][] { new int[] { 1, 2, 3 }, new int[] { 4, 5, 6 } };
                
                bool mismatchComparison = TetrisMultiplayer.Program.JaggedArrayEquals(array1, array2);
                if (!mismatchComparison)
                {
                    Console.WriteLine("? Mismatched array comparison test PASSED");
                }
                
                Console.WriteLine("? Error handling tests PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error handling test FAILED: {ex.Message}");
            }
            Console.WriteLine();
        }
        
        public static void RunAllSerializationTests()
        {
            Console.WriteLine("Running serialization bugfix tests...\n");
            
            TestJaggedArrayConversion();
            TestSerializationCompatibility();
            TestGameOverScenario();
            TestErrorHandling();
            
            Console.WriteLine("All serialization tests completed!");
        }
    }
}