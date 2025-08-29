using System;
using System.Collections.Generic;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.UI;

namespace TetrisMultiplayer.Tests
{
    public class ColorUIVisualizationTest
    {
        public static void RunColorTest()
        {
            Console.WriteLine("=== TESTING COLORFUL TETRIS UI ===");
            Console.WriteLine();

            // Test verschiedene Tetromino-Farben
            Console.WriteLine("Teste Tetromino-Farben:");
            foreach (TetrominoType type in Enum.GetValues<TetrominoType>())
            {
                Console.Write($"{type,-2}: ");
                
                // Erstelle eine einfache Engine mit dem Piece
                var engine = new TetrisEngine();
                engine.Current = new Tetromino(type);
                engine.Current.X = 2;
                engine.Current.Y = 2;
                
                // Zeige das Piece im Grid
                int[,] testGrid = new int[5, 10];
                foreach (var (x, y) in engine.Current.Blocks())
                {
                    if (y >= 0 && y < 5 && x >= 0 && x < 10)
                        testGrid[y, x] = (int)type + 1;
                }
                
                // Zeichne eine Zeile des Grids
                for (int x = 0; x < 10; x++)
                {
                    if (testGrid[2, x] == 0)
                        Console.Write(" .");
                    else
                    {
                        // Simuliere die WriteColoredBlock Methode
                        var blockValue = testGrid[2, x];
                        var pieceType = (TetrominoType)(blockValue - 1);
                        Console.ForegroundColor = GetColorForType(pieceType);
                        Console.Write("[]");
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();
            }
            
            Console.WriteLine();
            Console.WriteLine("Farbtest abgeschlossen!");
            Console.WriteLine("Drücke eine Taste um fortzufahren...");
        }
        
        private static ConsoleColor GetColorForType(TetrominoType type)
        {
            return type switch
            {
                TetrominoType.I => ConsoleColor.Cyan,
                TetrominoType.O => ConsoleColor.Yellow,
                TetrominoType.T => ConsoleColor.Magenta,
                TetrominoType.S => ConsoleColor.Green,
                TetrominoType.Z => ConsoleColor.Red,
                TetrominoType.J => ConsoleColor.Blue,
                TetrominoType.L => ConsoleColor.DarkYellow,
                _ => ConsoleColor.White
            };
        }
    }
}