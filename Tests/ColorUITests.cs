using System;
using System.Collections.Generic;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.UI;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class ColorUITests
    {
        [Fact]
        public void ColorUI_TetrominoColorsMapping_AllTypesHaveColors()
        {
            // Test: Stelle sicher, dass alle Tetromino-Typen Farben zugewiesen haben
            var tetrominoTypes = Enum.GetValues<TetrominoType>();
            
            foreach (TetrominoType type in tetrominoTypes)
            {
                // Dies sollte ohne Ausnahme funktionieren
                var engine = new TetrisEngine();
                engine.Current = new Tetromino(type);
                
                // Test dass die UI-Methoden ohne Fehler aufgerufen werden können
                var leaderboard = new List<(string, int, int, bool)>
                {
                    ("TestPlayer", 100, 100, false)
                };
                
                // Dies sollte keine Ausnahme werfen
                ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "TestPlayer", "Testing Colors", null, null, 1);
                
                Assert.True(true); // Test besteht wenn keine Ausnahme auftritt
            }
        }

        [Fact]
        public void ColorUI_ResetUI_ClearsColoredState()
        {
            // Test: UI Reset sollte auch bei farbiger UI korrekt funktionieren
            var engine = new TetrisEngine();
            engine.Current = new Tetromino(TetrominoType.I);
            
            var leaderboard = new List<(string, int, int, bool)>
            {
                ("Alice", 100, 100, true),
                ("Bob", 150, 50, false)
            };
            
            // Zeichne etwas mit Farben
            ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "TestPlayer", "Color Test", null, null, 1);
            
            // Reset und prüfe dass es ohne Fehler funktioniert
            ConsoleUI.ResetUI();
            
            // Sollte wieder zeichnen können
            ConsoleUI.DrawGameWithLeaderboard(engine, leaderboard, "TestPlayer", "After Reset", null, null, 1);
            
            Assert.True(true); // Test besteht wenn keine Ausnahme auftritt
        }

        [Fact]
        public void ColorUI_DrawFieldRaw_HandlesColoredBlocks()
        {
            // Test: DrawFieldRaw sollte farbige Blöcke korrekt handhaben
            int[,] testGrid = new int[5, 5];
            
            // Fülle Grid mit verschiedenen Tetromino-Typen
            testGrid[0, 0] = 1; // I-Piece
            testGrid[1, 1] = 2; // O-Piece
            testGrid[2, 2] = 3; // T-Piece
            testGrid[3, 3] = 4; // S-Piece
            testGrid[4, 4] = 5; // Z-Piece
            
            // Dies sollte ohne Ausnahme funktionieren
            ConsoleUI.DrawFieldRaw(testGrid);
            
            Assert.True(true); // Test besteht wenn keine Ausnahme auftritt
        }

        [Fact]
        public void ColorUI_ErrorHandling_GracefulFallback()
        {
            // Test: Bei ungültigen Werten sollte graceful fallback erfolgen
            try
            {
                int[,] invalidGrid = new int[2, 2];
                invalidGrid[0, 0] = 999; // Ungültiger Tetromino-Typ
                invalidGrid[1, 1] = -1;  // Negativer Wert
                
                // Sollte ohne Crash funktionieren (mit Fallback)
                ConsoleUI.DrawFieldRaw(invalidGrid);
                
                Assert.True(true); // Test erfolgreich wenn kein Crash
            }
            catch (Exception ex)
            {
                Assert.True(false, $"Unerwartete Ausnahme bei Fallback-Test: {ex.Message}");
            }
        }
    }
}