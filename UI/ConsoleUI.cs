using System;
using System.Collections.Generic;
using TetrisMultiplayer.Game;

namespace TetrisMultiplayer.UI
{
    public class ConsoleUI
    {
        // Farbschema für verschiedene Tetromino-Typen
        private static readonly Dictionary<TetrominoType, ConsoleColor> TetrominoColors = new()
        {
            { TetrominoType.I, ConsoleColor.Cyan },     // I-Piece: Cyan
            { TetrominoType.O, ConsoleColor.Yellow },   // O-Piece: Gelb
            { TetrominoType.T, ConsoleColor.Magenta },  // T-Piece: Magenta
            { TetrominoType.S, ConsoleColor.Green },    // S-Piece: Grün
            { TetrominoType.Z, ConsoleColor.Red },      // Z-Piece: Rot
            { TetrominoType.J, ConsoleColor.Blue },     // J-Piece: Blau
            { TetrominoType.L, ConsoleColor.DarkYellow } // L-Piece: Orange (DarkYellow)
        };

        // Standard-Farben für UI-Elemente
        private static readonly ConsoleColor BorderColor = ConsoleColor.White;
        private static readonly ConsoleColor ScoreColor = ConsoleColor.Green;
        private static readonly ConsoleColor StatusColor = ConsoleColor.Yellow;
        private static readonly ConsoleColor LeaderboardHeaderColor = ConsoleColor.Cyan;
        private static readonly ConsoleColor GameTitleColor = ConsoleColor.Magenta;
        private static int[,]? _lastRenderedGrid;
        private static int _lastScore = -1;
        private static string _lastStatusMsg = "";
        private static int? _lastRoundNumber = null;
        private static List<(string Name, int Score, int Hp, bool IsSpectator)>? _lastLeaderboard;
        private static bool _isInitialized = false;
        
        // Preview optimization caches
        private static TetrominoType? _lastPreviewType = null;
        private static int[,]? _lastPreviewGrid = null;

        /// <summary>
        /// Sichere Farbausgabe mit Ausnahmebehandlung - Utility-Methode für farbigen Text
        /// </summary>
        private static void WriteColored(string text, ConsoleColor? foreground = null, ConsoleColor? background = null)
        {
            try
            {
                var originalFg = Console.ForegroundColor;
                var originalBg = Console.BackgroundColor;
                
                if (foreground.HasValue) Console.ForegroundColor = foreground.Value;
                if (background.HasValue) Console.BackgroundColor = background.Value;
                
                Console.Write(text);
                
                // Farben zurücksetzen
                Console.ForegroundColor = originalFg;
                Console.BackgroundColor = originalBg;
            }
            catch (Exception)
            {
                // Bei Farbfehlern einfach ohne Farbe ausgeben
                Console.Write(text);
            }
        }

        /// <summary>
        /// Zeichnet einen farbigen Block basierend auf Tetromino-Typ
        /// </summary>
        private static void WriteColoredBlock(int blockValue)
        {
            try
            {
                if (blockValue == 0)
                {
                    Console.Write(" .");
                }
                else
                {
                    // Blockwert zu Tetromino-Typ konvertieren (blockValue = Type + 1)
                    var type = (TetrominoType)(blockValue - 1);
                    if (TetrominoColors.TryGetValue(type, out ConsoleColor color))
                    {
                        WriteColored("[]", color);
                    }
                    else
                    {
                        // Fallback ohne Farbe
                        Console.Write("[]");
                    }
                }
            }
            catch (Exception)
            {
                // Bei Fehlern Fallback ohne Farbe
                Console.Write(blockValue == 0 ? " ." : "[]");
            }
        }

        public static void RunSinglePlayer()
        {
            var engine = new TetrisEngine();
            engine.SpawnNext(); // For single player, spawn the first piece
            
            DateTime lastGravity = DateTime.Now;
            const int gravityDelayMs = 1000; // Piece falls every second
            
            while (true)
            {
                // Handle gravity (automatic falling)
                if (DateTime.Now.Subtract(lastGravity).TotalMilliseconds >= gravityDelayMs)
                {
                    if (!engine.Move(0, 1))
                    {
                        // Can't move down, place piece and spawn next
                        engine.Place();
                        engine.SpawnNext();
                    }
                    lastGravity = DateTime.Now;
                }

                DrawField(engine);
                // Draw next piece preview (4x4 grid) - optimized with colors
                int previewLeft = TetrisEngine.Width * 2 + 6;
                int previewTop = 3; // Start after "Next:" label
                Console.SetCursorPosition(previewLeft, 2);
                WriteColored("Next:", StatusColor);
                DrawOptimizedPreview(engine.Next, previewLeft, previewTop);
                
                Console.SetCursorPosition(0, TetrisEngine.Height + 2);
                WriteColored($"Score: {engine.Score}", ScoreColor);
                Console.WriteLine();
                WriteColored($"Next: {engine.Next.Type}", StatusColor);
                Console.WriteLine();
                Console.WriteLine("Steuerung: Links/Rechts/Unten/Hoch, Z/X drehen, Leertaste HardDrop, Q quit");
                
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    bool moved = false;
                    if (key.Key == ConsoleKey.LeftArrow) moved = engine.Move(-1, 0);
                    else if (key.Key == ConsoleKey.RightArrow) moved = engine.Move(1, 0);
                    else if (key.Key == ConsoleKey.DownArrow) 
                    {
                        if (!engine.Move(0, 1))
                        {
                            engine.Place();
                            engine.SpawnNext();
                        }
                        moved = true;
                    }
                    else if (key.Key == ConsoleKey.UpArrow) { engine.Rotate(1); moved = true; } // Up arrow for rotation
                    else if (key.Key == ConsoleKey.Spacebar) 
                    {
                        engine.HardDrop();
                        engine.SpawnNext();
                        moved = true;
                    }
                    else if (key.Key == ConsoleKey.Z) { engine.Rotate(-1); moved = true; }
                    else if (key.Key == ConsoleKey.X) { engine.Rotate(1); moved = true; }
                    else if (key.Key == ConsoleKey.Q) break;
                    
                    if (moved)
                    {
                        // Reset gravity timer on player input
                        lastGravity = DateTime.Now;
                    }
                }
                
                System.Threading.Thread.Sleep(50); // Smooth updates
            }
        }

        public static void DrawField(TetrisEngine engine)
        {
            if (!_isInitialized)
            {
                Console.Clear();
                _isInitialized = true;
            }

            int h = TetrisEngine.Height, w = TetrisEngine.Width;
            int[,] currentGrid = new int[h, w];
            Array.Copy(engine.Grid, currentGrid, engine.Grid.Length);
            
            // Add current piece to grid for rendering
            if (engine.Current != null)
            {
                foreach (var (x, y) in engine.Current.Blocks())
                {
                    if (y >= 0 && y < h && x >= 0 && x < w)
                        currentGrid[y, x] = (int)engine.Current.Type + 1;
                }
            }

            // Only redraw cells that have changed - now with colors
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (_lastRenderedGrid == null || _lastRenderedGrid[y, x] != currentGrid[y, x])
                    {
                        Console.SetCursorPosition(x * 2 + 1, y); // +1 for left border
                        WriteColoredBlock(currentGrid[y, x]);
                    }
                }
            }

            // Draw colored borders only if not drawn before
            if (_lastRenderedGrid == null)
            {
                try
                {
                    // Farbige Ränder zeichnen
                    Console.ForegroundColor = BorderColor;
                    for (int y = 0; y < h; y++)
                    {
                        Console.SetCursorPosition(0, y);
                        Console.Write("|");
                        Console.SetCursorPosition(w * 2 + 1, y);
                        Console.Write("|");
                    }
                    Console.SetCursorPosition(0, h);
                    Console.Write("+" + new string('-', w * 2) + "+");
                    Console.ResetColor();
                }
                catch (Exception)
                {
                    // Fallback ohne Farbe
                    for (int y = 0; y < h; y++)
                    {
                        Console.SetCursorPosition(0, y);
                        Console.Write("|");
                        Console.SetCursorPosition(w * 2 + 1, y);
                        Console.Write("|");
                    }
                    Console.SetCursorPosition(0, h);
                    Console.Write("+" + new string('-', w * 2) + "+");
                }
            }

            _lastRenderedGrid = currentGrid;
        }

        public static void DrawGameWithLeaderboard(TetrisEngine engine, List<(string Name, int Score, int Hp, bool IsSpectator)> leaderboard, string selfName, string statusMsg = "", Dictionary<string, string>? playerNames = null, HashSet<string>? playersWhoPlaced = null, int? roundNumber = null)
        {
            int h = TetrisEngine.Height, w = TetrisEngine.Width;
            int fieldLeft = 2, fieldTop = 3; // Increased top margin to prevent overlap
            int boardWidth = w * 2 + 2;
            int leaderboardLeft = fieldLeft + boardWidth + 12; // Shift leaderboard right for next piece preview
            int leaderboardTop = fieldTop;
            int nextPieceLeft = fieldLeft + boardWidth + 2;
            int nextPieceTop = fieldTop + 2;
            
            // Initialize if first time
            if (!_isInitialized)
            {
                Console.Clear();
                _isInitialized = true;
                _lastRenderedGrid = null;
                _lastLeaderboard = null;
                _lastScore = -1;
                _lastStatusMsg = "";
                _lastRoundNumber = null;
                
                // Draw colored game title at top
                Console.SetCursorPosition(fieldLeft, 0);
                WriteColored("=== TETRIS MULTIPLAYER ===", GameTitleColor);
            }

            // Create current grid with piece
            int[,] currentGrid = new int[h, w];
            Array.Copy(engine.Grid, currentGrid, engine.Grid.Length);
            
            if (engine.Current != null)
            {
                foreach (var (x, y) in engine.Current.Blocks())
                {
                    if (y >= 0 && y < h && x >= 0 && x < w)
                        currentGrid[y, x] = (int)engine.Current.Type + 1;
                }
            }

            // Only redraw field cells that have changed - now with colors
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (_lastRenderedGrid == null || _lastRenderedGrid[y, x] != currentGrid[y, x])
                    {
                        Console.SetCursorPosition(fieldLeft + x * 2 + 1, fieldTop + y);
                        WriteColoredBlock(currentGrid[y, x]);
                    }
                }
            }

            // Draw colored field borders only once
            if (_lastRenderedGrid == null)
            {
                try
                {
                    Console.ForegroundColor = BorderColor;
                    for (int y = 0; y < h; y++)
                    {
                        Console.SetCursorPosition(fieldLeft, fieldTop + y);
                        Console.Write("|");
                        Console.SetCursorPosition(fieldLeft + w * 2 + 1, fieldTop + y);
                        Console.Write("|");
                    }
                    Console.SetCursorPosition(fieldLeft, fieldTop + h);
                    Console.Write("+" + new string('-', w * 2) + "+");
                    Console.ResetColor();
                }
                catch (Exception)
                {
                    // Fallback ohne Farbe
                    for (int y = 0; y < h; y++)
                    {
                        Console.SetCursorPosition(fieldLeft, fieldTop + y);
                        Console.Write("|");
                        Console.SetCursorPosition(fieldLeft + w * 2 + 1, fieldTop + y);
                        Console.Write("|");
                    }
                    Console.SetCursorPosition(fieldLeft, fieldTop + h);
                    Console.Write("+" + new string('-', w * 2) + "+");
                }
            }

            // Draw colored next piece preview
            if (engine.Next != null)
            {
                Console.SetCursorPosition(nextPieceLeft, fieldTop);
                WriteColored("Next:", StatusColor);
                DrawOptimizedPreview(engine.Next, nextPieceLeft, nextPieceTop);
            }

            // Update colored score only if changed
            if (_lastScore != engine.Score)
            {
                Console.SetCursorPosition(fieldLeft, fieldTop + h + 2);
                WriteColored($"Score: {engine.Score}".PadRight(20), ScoreColor);
                _lastScore = engine.Score;
            }

            // Enhanced leaderboard with colors and real-time status
            if (_lastLeaderboard == null || !LeaderboardEquals(_lastLeaderboard, leaderboard) || playersWhoPlaced != null)
            {
                Console.SetCursorPosition(leaderboardLeft, leaderboardTop);
                WriteColored("Real-time Leaderboard:".PadRight(40), LeaderboardHeaderColor);
                Console.SetCursorPosition(leaderboardLeft, leaderboardTop + 1);
                WriteColored("Name        Score   HP   Status      State".PadRight(40), LeaderboardHeaderColor);
                
                int row = 2;
                foreach (var entry in leaderboard)
                {
                    Console.SetCursorPosition(leaderboardLeft, leaderboardTop + row);
                    
                    // Get real player name if available
                    string displayName = entry.Name;
                    if (playerNames != null && playerNames.ContainsKey(entry.Name))
                    {
                        displayName = playerNames[entry.Name];
                    }
                    if (displayName.Length > 10) displayName = displayName.Substring(0, 10);
                    
                    string status = entry.IsSpectator ? "Spectator" : "Playing";
                    string marker = entry.Name == selfName ? ">" : " ";
                    
                    // Real-time state indicator
                    string state = "Waiting";
                    if (playersWhoPlaced != null && playersWhoPlaced.Contains(entry.Name))
                    {
                        state = "Done";
                    }
                    else if (!entry.IsSpectator)
                    {
                        state = "Active";
                    }
                    
                    // Farbige Leaderboard-Einträge
                    ConsoleColor entryColor = entry.IsSpectator ? ConsoleColor.Gray : 
                                            (entry.Name == selfName ? ConsoleColor.Yellow : ConsoleColor.White);
                    
                    string line = $"{marker}{displayName,-10} {entry.Score,5} {entry.Hp,3} {status,-9} {state,-6}";
                    WriteColored(line.PadRight(40), entryColor);
                    row++;
                }
                
                // Clear any extra lines from previous leaderboard
                if (_lastLeaderboard != null)
                {
                    for (int i = leaderboard.Count; i < _lastLeaderboard.Count + 2; i++)
                    {
                        Console.SetCursorPosition(leaderboardLeft, leaderboardTop + 2 + i);
                        Console.Write("".PadRight(40));
                    }
                }
                
                _lastLeaderboard = new List<(string, int, int, bool)>(leaderboard);
            }

            // Update colored round number display (always visible, separate from status)
            if (roundNumber.HasValue && _lastRoundNumber != roundNumber.Value)
            {
                Console.SetCursorPosition(fieldLeft, fieldTop + h + 3);
                WriteColored($"Round: {roundNumber.Value}".PadRight(20), ConsoleColor.Cyan);
                _lastRoundNumber = roundNumber.Value;
            }

            // Update colored status message only if changed
            if (_lastStatusMsg != statusMsg)
            {
                Console.SetCursorPosition(fieldLeft, fieldTop + h + 4);
                WriteColored(statusMsg.PadRight(60), StatusColor);
                _lastStatusMsg = statusMsg;
            }

            _lastRenderedGrid = currentGrid;
        }

        public static void DrawGameWithLeaderboard(TetrisEngine engine, List<(string Name, int Score, int Hp, bool IsSpectator)> leaderboard, string selfName, string statusMsg = "")
        {
            DrawGameWithLeaderboard(engine, leaderboard, selfName, statusMsg, null, null, null);
        }

        private static bool LeaderboardEquals(List<(string Name, int Score, int Hp, bool IsSpectator)> a, 
                                            List<(string Name, int Score, int Hp, bool IsSpectator)> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Name != b[i].Name || a[i].Score != b[i].Score || 
                    a[i].Hp != b[i].Hp || a[i].IsSpectator != b[i].IsSpectator)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Modular piece visualization helper to calculate optimal preview dimensions and centering
        /// </summary>
        public static class PieceVisualizationHelper
        {
            /// <summary>
            /// Calculate the bounding box of a piece in a specific rotation
            /// </summary>
            public static (int width, int height, int minX, int minY, int maxX, int maxY) GetPieceBounds(TetrominoType type, int rotation = 0)
            {
                var piece = new Tetromino(type);
                var blocks = piece.Blocks(0, 0, rotation);
                
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;
                
                foreach (var (x, y) in blocks)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
                
                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                
                return (width, height, minX, minY, maxX, maxY);
            }
            
            /// <summary>
            /// Calculate optimal preview size for any piece type (modular for future expansion)
            /// </summary>
            public static int GetOptimalPreviewSize(TetrominoType type)
            {
                // Calculate required size for all rotations of this piece
                int maxDimension = 0;
                
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var (width, height, _, _, _, _) = GetPieceBounds(type, rotation);
                    maxDimension = Math.Max(maxDimension, Math.Max(width, height));
                }
                
                // Add padding for centering and visual clarity
                return Math.Max(4, maxDimension + 1);
            }
            
            /// <summary>
            /// Calculate proper centering position for a piece in a preview grid
            /// </summary>
            public static (int x, int y) GetOptimalCenterPosition(TetrominoType type, int previewSize, int rotation = 0)
            {
                var (width, height, minX, minY, maxX, maxY) = GetPieceBounds(type, rotation);
                
                // Calculate center position to place the piece in the middle of the preview grid
                int centerX = (previewSize - width) / 2 - minX;
                int centerY = (previewSize - height) / 2 - minY;
                
                return (centerX, centerY);
            }
        }
        
        private static (int x, int y) GetPieceCenterPosition(TetrominoType type)
        {
            // Use the new modular system for better centering
            return PieceVisualizationHelper.GetOptimalCenterPosition(type, 4, 0);
        }
        
        private static void DrawOptimizedPreview(Tetromino nextPiece, int previewLeft, int previewTop)
        {
            // Only redraw if the next piece has changed
            if (nextPiece != null && (_lastPreviewType != nextPiece.Type || _lastPreviewGrid == null))
            {
                // Use modular system to determine optimal preview size for this piece type
                int previewSize = PieceVisualizationHelper.GetOptimalPreviewSize(nextPiece.Type);
                
                // Clear previous preview area (use larger area to ensure cleanup)
                int maxClearSize = _lastPreviewGrid?.GetLength(0) ?? previewSize;
                for (int py = 0; py < Math.Max(previewSize, maxClearSize); py++)
                {
                    Console.SetCursorPosition(previewLeft, previewTop + py);
                    Console.Write(new string(' ', Math.Max(previewSize, maxClearSize) * 2));
                }
                
                // Create and cache new preview with optimal size
                _lastPreviewGrid = new int[previewSize, previewSize];
                _lastPreviewType = nextPiece.Type;
                
                // Get optimal centering position using modular system
                var (centerX, centerY) = PieceVisualizationHelper.GetOptimalCenterPosition(nextPiece.Type, previewSize, 0);
                
                // Place piece blocks in the preview grid
                foreach (var (x, y) in nextPiece.Blocks(centerX, centerY, 0))
                {
                    if (x >= 0 && x < previewSize && y >= 0 && y < previewSize)
                        _lastPreviewGrid[y, x] = (int)nextPiece.Type + 1;
                }
                
                // Draw the cached preview with colors
                for (int py = 0; py < previewSize; py++)
                {
                    Console.SetCursorPosition(previewLeft, previewTop + py);
                    for (int px = 0; px < previewSize; px++)
                    {
                        if (_lastPreviewGrid[py, px] == 0)
                        {
                            Console.Write("  ");
                        }
                        else
                        {
                            // Farbige Vorschau-Blöcke
                            WriteColoredBlock(_lastPreviewGrid[py, px]);
                        }
                    }
                }
            }
        }

        public static void ResetUI()
        {
            _lastRenderedGrid = null;
            _lastScore = -1;
            _lastStatusMsg = "";
            _lastRoundNumber = null;
            _lastLeaderboard = null;
            _isInitialized = false;
            // Reset preview cache
            _lastPreviewType = null;
            _lastPreviewGrid = null;
        }

        public static void DrawFieldRaw(int[,] grid)
        {
            int h = grid.GetLength(0), w = grid.GetLength(1);
            try
            {
                Console.ForegroundColor = BorderColor;
                for (int y = 0; y < h; y++)
                {
                    Console.Write("|");
                    Console.ResetColor();
                    for (int x = 0; x < w; x++)
                    {
                        WriteColoredBlock(grid[y, x]);
                    }
                    Console.ForegroundColor = BorderColor;
                    Console.WriteLine("|");
                }
                Console.WriteLine("+" + new string('-', w * 2) + "+");
                Console.ResetColor();
            }
            catch (Exception)
            {
                // Fallback ohne Farbe
                for (int y = 0; y < h; y++)
                {
                    Console.Write("|");
                    for (int x = 0; x < w; x++)
                    {
                        Console.Write(grid[y, x] == 0 ? " ." : "[]");
                    }
                    Console.WriteLine("|");
                }
                Console.WriteLine("+" + new string('-', w * 2) + "+");
            }
        }
    }
}
