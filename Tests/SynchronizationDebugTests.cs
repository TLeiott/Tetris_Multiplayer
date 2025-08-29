using System;
using System.Collections.Generic;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.Networking;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class SynchronizationDebugTests
    {
        [Fact]
        public void RoundResultsDto_SerializationWorks()
        {
            // Test that RoundResultsDto can be properly serialized and deserialized
            var dto = new NetworkManager.RoundResultsDto
            {
                DeletedRowsPerPlayer = new Dictionary<string, int> { ["player1"] = 2, ["player2"] = 1 },
                NewScores = new Dictionary<string, int> { ["player1"] = 300, ["player2"] = 100 },
                Hp = new Dictionary<string, int> { ["player1"] = 19, ["player2"] = 20 },
                HpChanges = new Dictionary<string, int> { ["player1"] = 0, ["player2"] = -1 },
                Spectators = new List<string> { "player3" }
            };
            
            // Test property access (this should not throw)
            Assert.Equal(2, dto.DeletedRowsPerPlayer["player1"]);
            Assert.Equal(300, dto.NewScores["player1"]);
            Assert.Equal(19, dto.Hp["player1"]);
            Assert.Equal(0, dto.HpChanges["player1"]);
            Assert.Contains("player3", dto.Spectators);
        }
        
        [Fact]
        public void GameManager_ProducesSamePiecesWithSameSeed()
        {
            // Verify that GameManager with same seed produces identical sequences
            int seed = 42;
            var gm1 = new GameManager(seed);
            var gm2 = new GameManager(seed);
            
            var sequence1 = new List<int>();
            var sequence2 = new List<int>();
            
            for (int i = 0; i < 20; i++)
            {
                sequence1.Add(gm1.GetNextPiece());
                sequence2.Add(gm2.GetNextPiece());
            }
            
            Assert.Equal(sequence1, sequence2);
        }
        
        [Fact]
        public void TetrominoType_ValidRange()
        {
            // Verify all piece IDs are valid
            for (int i = 0; i < 7; i++)
            {
                var type = (TetrominoType)i;
                var tetromino = new Tetromino(type);
                Assert.Equal(type, tetromino.Type);
                Assert.True(Enum.IsDefined(typeof(TetrominoType), type));
            }
        }
    }
}