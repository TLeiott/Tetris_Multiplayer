using System;
using System.Collections.Generic;

namespace TetrisMultiplayer.Game
{
    public class GameManager
    {
        private readonly int _seed;
        private readonly Random _rng;
        private readonly List<int> _pieceSequence = new();
        private int _currentIndex = 0;

        public GameManager(int? seed = null)
        {
            _seed = seed ?? Environment.TickCount;
            _rng = new Random(_seed);
            
            // Pre-generate a large sequence of pieces to ensure synchronization
            GenerateInitialSequence(100);
        }

        public int Seed => _seed;

        private void GenerateInitialSequence(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _pieceSequence.Add(_rng.Next(0, 7)); // 0-6 for 7 Tetris pieces
            }
        }

        // Generate next piece deterministically
        public int GetNextPiece()
        {
            if (_currentIndex >= _pieceSequence.Count)
            {
                // If we've exhausted the pre-generated sequence, generate more
                GenerateInitialSequence(50);
            }
            
            int piece = _pieceSequence[_currentIndex];
            _currentIndex++;
            return piece;
        }

        // For debugging: get the next piece without advancing
        public int PeekNextPiece()
        {
            if (_currentIndex >= _pieceSequence.Count)
            {
                GenerateInitialSequence(50);
            }
            return _pieceSequence[_currentIndex];
        }

        // For multiplayer: get the piece after the next one (for preview display)
        public int PeekPreviewPiece()
        {
            if (_currentIndex + 1 >= _pieceSequence.Count)
            {
                GenerateInitialSequence(50);
            }
            return _pieceSequence[_currentIndex + 1];
        }

        // For host: get full sequence so far
        public IReadOnlyList<int> PieceSequence => _pieceSequence.AsReadOnly();

        // For client: set sequence from host (not used in current implementation)
        public void SetSequence(List<int> sequence)
        {
            _pieceSequence.Clear();
            _pieceSequence.AddRange(sequence);
            _currentIndex = sequence.Count;
        }

        // Reset to beginning of sequence (for game restart)
        public void Reset()
        {
            _currentIndex = 0;
        }

        // Get current position in sequence (for debugging)
        public int CurrentIndex => _currentIndex;
    }
}
