using System;
using TetrisMultiplayer.Game;

namespace TetrisMultiplayer.Tests
{
    public static class SimpleColorDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("=== COLORFUL TETRIS UI DEMONSTRATION ===");
            Console.WriteLine();
            
            Console.WriteLine("Tetromino Colors:");
            ShowTetrominoColors();
            
            Console.WriteLine();
            Console.WriteLine("Sample Colored Game Field:");
            ShowColoredGameField();
            
            Console.WriteLine();
            Console.WriteLine("UI Elements with Colors:");
            ShowUIElements();
            
            Console.WriteLine();
            Console.WriteLine("=== COLOR DEMO COMPLETE ===");
        }
        
        private static void ShowTetrominoColors()
        {
            var colors = new (TetrominoType type, ConsoleColor color, string name)[]
            {
                (TetrominoType.I, ConsoleColor.Cyan, "Cyan"),
                (TetrominoType.O, ConsoleColor.Yellow, "Yellow"),
                (TetrominoType.T, ConsoleColor.Magenta, "Magenta"),
                (TetrominoType.S, ConsoleColor.Green, "Green"),
                (TetrominoType.Z, ConsoleColor.Red, "Red"),
                (TetrominoType.J, ConsoleColor.Blue, "Blue"),
                (TetrominoType.L, ConsoleColor.DarkYellow, "Orange")
            };
            
            foreach (var (type, color, name) in colors)
            {
                Console.Write($"  {type}: ");
                Console.ForegroundColor = color;
                Console.Write("████");
                Console.ResetColor();
                Console.WriteLine($" ({name})");
            }
        }
        
        private static void ShowColoredGameField()
        {
            // Simulated mini game field with different colored pieces
            var field = new (int value, ConsoleColor color)[][]
            {
                new[] { (0, ConsoleColor.White), (0, ConsoleColor.White), (1, ConsoleColor.Cyan), (1, ConsoleColor.Cyan), (0, ConsoleColor.White) },
                new[] { (0, ConsoleColor.White), (2, ConsoleColor.Yellow), (2, ConsoleColor.Yellow), (0, ConsoleColor.White), (0, ConsoleColor.White) },
                new[] { (3, ConsoleColor.Magenta), (3, ConsoleColor.Magenta), (3, ConsoleColor.Magenta), (0, ConsoleColor.White), (0, ConsoleColor.White) },
                new[] { (4, ConsoleColor.Green), (4, ConsoleColor.Green), (5, ConsoleColor.Red), (5, ConsoleColor.Red), (0, ConsoleColor.White) },
                new[] { (6, ConsoleColor.Blue), (7, ConsoleColor.DarkYellow), (7, ConsoleColor.DarkYellow), (7, ConsoleColor.DarkYellow), (0, ConsoleColor.White) }
            };
            
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("+----------+");
            Console.ResetColor();
            
            foreach (var row in field)
            {
                Console.Write("  ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("|");
                Console.ResetColor();
                
                foreach (var (value, color) in row)
                {
                    if (value == 0)
                    {
                        Console.Write(" .");
                    }
                    else
                    {
                        Console.ForegroundColor = color;
                        Console.Write("[]");
                        Console.ResetColor();
                    }
                }
                
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("|");
                Console.ResetColor();
            }
            
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("+----------+");
            Console.ResetColor();
        }
        
        private static void ShowUIElements()
        {
            Console.Write("Score: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("1250");
            Console.ResetColor();
            
            Console.Write("Status: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Round 5 - Active");
            Console.ResetColor();
            
            Console.Write("Title: ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("=== TETRIS MULTIPLAYER ===");
            Console.ResetColor();
            
            Console.Write("Leaderboard Header: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Real-time Leaderboard");
            Console.ResetColor();
        }
    }
}