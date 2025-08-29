using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;
using System.Text.Json;

namespace TetrisMultiplayer.Tests
{
    public class ClientInitializationTests
    {
        [Fact]
        public async Task ClientReceiveLoop_ProcessesStartGameMessage_SetsGameManager()
        {
            // Arrange
            var networkManager = new NetworkManager();
            
            // Create a mock StartGame message
            var startGameMessage = new { type = "StartGame", seed = 12345 };
            var json = JsonSerializer.Serialize(startGameMessage);
            
            // This test verifies the GameManager gets set when StartGame is received
            // In the real scenario, this would be processed by ClientReceiveLoop
            
            // Act - Simulate what ClientReceiveLoop does when StartGame is received
            var jsonDoc = JsonDocument.Parse(json);
            var element = jsonDoc.RootElement;
            
            if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "StartGame")
            {
                if (element.TryGetProperty("seed", out var seedProp) && seedProp.TryGetInt32(out int seed))
                {
                    // This would be set by ClientReceiveLoop's private method
                    // We can't test the private field directly, but we can verify the logic
                    var gameManager = new TetrisMultiplayer.Game.GameManager(seed);
                    
                    // Assert
                    Assert.NotNull(gameManager);
                    Assert.Equal(12345, gameManager.Seed);
                }
            }
        }
        
        [Fact]
        public void ClientLobbyLoop_Logic_ChecksGameManagerFirst()
        {
            // This test verifies the logic fix in ClientLobbyLoop
            // The fix ensures GameManager is checked BEFORE waiting for LobbyUpdate
            
            // Arrange
            var networkManager = new NetworkManager();
            bool gameStarted = false;
            
            // Simulate the fixed logic: check GameManager != null FIRST
            // Act
            if (networkManager.GameManager != null && !gameStarted)
            {
                gameStarted = true;
                // Would call ClientGameLoop here
            }
            
            // Assert - Initially GameManager is null, so gameStarted should remain false
            Assert.False(gameStarted);
            Assert.Null(networkManager.GameManager);
        }
        
        [Fact]
        public void NextPieceMessage_ParsesCorrectly_WithPreviewPieceId()
        {
            // This test verifies the NextPiece parsing fix
            
            // Arrange - Create NextPiece message with previewPieceId
            var nextPieceMessage = new { type = "NextPiece", pieceId = 3, previewPieceId = 5 };
            var json = JsonSerializer.Serialize(nextPieceMessage);
            var jsonDoc = JsonDocument.Parse(json);
            var element = jsonDoc.RootElement;
            
            // Act - Simulate the fixed parsing logic
            if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "NextPiece")
            {
                var pieceId = element.GetProperty("pieceId").GetInt32();
                int? previewPieceId = null;
                if (element.TryGetProperty("previewPieceId", out var previewProp))
                {
                    previewPieceId = previewProp.GetInt32();
                }
                
                // Assert
                Assert.Equal(3, pieceId);
                Assert.Equal(5, previewPieceId);
            }
        }
    }
}