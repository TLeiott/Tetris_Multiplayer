using System;
using System.Collections.Generic;

namespace TetrisMultiplayer.Game
{
    public enum TetrominoType { I, O, T, S, Z, J, L }

    public class TetrisEngine
    {
        public const int Width = 10;
        public const int Height = 20;
        public int[,] Grid { get; } = new int[Height, Width];
        public Tetromino? Current { get; set; } // Nullable made explicit
        public Tetromino Next { get; set; }
        public int Score { get; private set; }
        private Random _rng;

        public TetrisEngine(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            Next = GenerateTetromino();
            // Don't auto-spawn in multiplayer mode - let the game logic control this
            Current = null;
        }

        public void SpawnNext()
        {
            Current = Next;
            Next = GenerateTetromino();
            if (Current != null)
            {
                Current.X = Width / 2 - 2;
                Current.Y = 0;
            }
        }

        // For multiplayer: set the next piece externally (synchronized)
        public void SetNextPiece(Tetromino nextPiece)
        {
            Next = nextPiece;
        }

        // For multiplayer: set the next piece by type (synchronized)
        public void SetNextPiece(TetrominoType type)
        {
            Next = new Tetromino(type);
        }

        public Tetromino GenerateTetromino()
        {
            var type = (TetrominoType)_rng.Next(0, 7);
            return new Tetromino(type);
        }

        public bool Move(int dx, int dy)
        {
            if (Current == null) return false;
            
            if (IsValid(Current, Current.X + dx, Current.Y + dy, Current.Rotation))
            {
                Current.X += dx;
                Current.Y += dy;
                return true;
            }
            return false;
        }

        public void HardDrop()
        {
            if (Current == null) return;
            
            while (Move(0, 1)) { }
            Place();
        }

        public void Rotate(int dir)
        {
            if (Current == null) return;
            
            int newRot = (Current.Rotation + dir + 4) % 4;
            // Simple SRS kicks (only basic)
            int[,] kicks = { {0,0}, {1,0}, {-1,0}, {0,1}, {0,-1} };
            for (int i = 0; i < kicks.GetLength(0); i++)
            {
                int nx = Current.X + kicks[i,0];
                int ny = Current.Y + kicks[i,1];
                if (IsValid(Current, nx, ny, newRot))
                {
                    Current.X = nx;
                    Current.Y = ny;
                    Current.Rotation = newRot;
                    break;
                }
            }
        }

        public void Place()
        {
            if (Current == null) return;
            
            foreach (var (x, y) in Current.Blocks())
            {
                if (y >= 0 && y < Height && x >= 0 && x < Width)
                    Grid[y, x] = (int)Current.Type + 1;
            }
            int lines = ClearLines();
            Score += lines switch { 1 => 100, 2 => 300, 3 => 500, 4 => 800, _ => 0 };
            // In multiplayer, don't auto-spawn next piece - let game logic control this
            Current = null;
        }

        public int ClearLines()
        {
            int lines = 0;
            for (int y = Height - 1; y >= 0; y--)
            {
                bool full = true;
                for (int x = 0; x < Width; x++)
                    if (Grid[y, x] == 0) { full = false; break; }
                if (full)
                {
                    for (int yy = y; yy > 0; yy--)
                        for (int x = 0; x < Width; x++)
                            Grid[yy, x] = Grid[yy - 1, x];
                    for (int x = 0; x < Width; x++)
                        Grid[0, x] = 0;
                    lines++;
                    y++; // check same line again
                }
            }
            return lines;
        }

        public bool IsValid(Tetromino? t, int x, int y, int rot)
        {
            if (t == null) return false;
            
            foreach (var (bx, by) in t.Blocks(x, y, rot))
            {
                if (bx < 0 || bx >= Width || by < 0 || by >= Height) return false;
                if (by >= 0 && Grid[by, bx] != 0) return false;
            }
            return true;
        }

        // Helper method to get the current game field (for serialization)
        public int[,] GetGameField()
        {
            return (int[,])Grid.Clone();
        }
    }

    public class Tetromino
    {
        public TetrominoType Type { get; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Rotation { get; set; }
        public Tetromino(TetrominoType type)
        {
            Type = type;
            X = 3; Y = 0; Rotation = 0;
        }
        public IEnumerable<(int x, int y)> Blocks() => Blocks(X, Y, Rotation);
        public IEnumerable<(int x, int y)> Blocks(int x, int y, int rot)
        {
            // SRS shapes
            int[,,] shapes = new int[7,4,8]
            {
                // I
                { {0,1,1,1,2,1,3,1}, {2,0,2,1,2,2,2,3}, {0,2,1,2,2,2,3,2}, {1,0,1,1,1,2,1,3} },
                // O
                { {1,0,2,0,1,1,2,1}, {1,0,2,0,1,1,2,1}, {1,0,2,0,1,1,2,1}, {1,0,2,0,1,1,2,1} },
                // T
                { {1,0,0,1,1,1,2,1}, {1,0,1,1,2,1,1,2}, {0,1,1,1,2,1,1,2}, {1,0,0,1,1,1,1,2} },
                // S
                { {1,0,2,0,0,1,1,1}, {1,0,1,1,2,1,2,2}, {1,1,2,1,0,2,1,2}, {0,0,0,1,1,1,1,2} },
                // Z
                { {0,0,1,0,1,1,2,1}, {2,0,1,1,2,1,1,2}, {0,1,1,1,1,2,2,2}, {1,0,0,1,1,1,0,2} },
                // J
                { {0,0,0,1,1,1,2,1}, {1,0,2,0,1,1,1,2}, {0,1,1,1,2,1,2,2}, {1,0,1,1,0,2,1,2} },
                // L
                { {2,0,0,1,1,1,2,1}, {1,0,1,1,1,2,2,2}, {0,1,1,1,2,1,0,2}, {0,0,1,0,1,1,1,2} }
            };
            int t = (int)Type;
            for (int i = 0; i < 4; i++)
                yield return (x + shapes[t, rot, i * 2], y + shapes[t, rot, i * 2 + 1]);
        }
    }
}
