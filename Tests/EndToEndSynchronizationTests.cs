using System;
using System.Linq;
using System.Threading.Tasks;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.Networking;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class EndToEndSynchronizationTests
    {
        [Fact]
        public void RoundResultsDto_ContainsMethodWorks()
        {
            // Verify the Contains method works properly for spectator checking
            var dto = new NetworkManager.RoundResultsDto();
            dto.Spectators.Add("player1");
            dto.Spectators.Add("player2");
            
            Assert.Contains("player1", dto.Spectators);
            Assert.Contains("player2", dto.Spectators);
            Assert.DoesNotContain("player3", dto.Spectators);
            // Verify list doesn't contain null values
            Assert.DoesNotContain(dto.Spectators, item => item == null);
        }
        
        [Fact]
        public void GameManager_SeedConsistency()
        {
            // Test that the same seed always produces the same first 10 pieces
            int seed = 12345;
            
            var expectedSequence = new int[10];
            var gm1 = new GameManager(seed);
            for (int i = 0; i < 10; i++)
            {
                expectedSequence[i] = gm1.GetNextPiece();
            }
            
            // Test multiple times with same seed
            for (int trial = 0; trial < 5; trial++)
            {
                var gm = new GameManager(seed);
                for (int i = 0; i < 10; i++)
                {
                    int piece = gm.GetNextPiece();
                    Assert.Equal(expectedSequence[i], piece);
                    Assert.InRange(piece, 0, 6); // Valid piece range
                }
            }
        }
        
        [Fact]
        public void PieceId_ToTetrominoType_Conversion()
        {
            // Test that all piece IDs convert properly to TetrominoType
            for (int pieceId = 0; pieceId < 7; pieceId++)
            {
                var type = (TetrominoType)pieceId;
                var tetromino = new Tetromino(type);
                
                Assert.Equal(type, tetromino.Type);
                Assert.True(tetromino.Blocks().Count() >= 4);
            }
        }
    }
}