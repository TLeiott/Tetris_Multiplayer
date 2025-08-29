using System;
using System.Collections.Generic;
using System.Linq;
using TetrisMultiplayer.Game;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class PieceSynchronizationTests
    {
        [Fact]
        public void GameManager_WithSameSeed_GeneratesSamePieceSequence()
        {
            // Arrange
            int seed = 12345;
            var gm1 = new GameManager(seed);
            var gm2 = new GameManager(seed);
            
            // Act & Assert
            for (int i = 0; i < 10; i++)
            {
                int piece1 = gm1.GetNextPiece();
                int piece2 = gm2.GetNextPiece();
                Assert.Equal(piece1, piece2);
            }
        }
        
        [Fact]
        public void GameManager_WithDifferentSeeds_GeneratesDifferentSequences()
        {
            // Arrange
            var gm1 = new GameManager(12345);
            var gm2 = new GameManager(54321);
            
            // Act
            var sequence1 = new List<int>();
            var sequence2 = new List<int>();
            
            for (int i = 0; i < 10; i++)
            {
                sequence1.Add(gm1.GetNextPiece());
                sequence2.Add(gm2.GetNextPiece());
            }
            
            // Assert - sequences should be different
            Assert.NotEqual(sequence1, sequence2);
        }
        
        [Fact]
        public void Tetromino_AllTypesValid()
        {
            // Test that all piece types can be created
            for (int i = 0; i < 7; i++)
            {
                var type = (TetrominoType)i;
                var tetro = new Tetromino(type);
                Assert.Equal(type, tetro.Type);
                Assert.True(tetro.Blocks().Count() >= 4); // All pieces have 4 blocks
            }
        }
        
        [Fact]
        public void TetrisEngine_RotationWorksCorrectly()
        {
            // Arrange
            var engine = new TetrisEngine();
            engine.Current = new Tetromino(TetrominoType.T);
            int initialRotation = engine.Current.Rotation;
            
            // Act
            engine.Rotate(1);
            
            // Assert
            Assert.NotEqual(initialRotation, engine.Current.Rotation);
        }
    }
}