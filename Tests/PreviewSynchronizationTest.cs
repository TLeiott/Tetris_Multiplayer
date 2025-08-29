using System;
using TetrisMultiplayer.Game;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class PreviewSynchronizationTest
    {
        [Fact]
        public void GameManager_PeekPreviewPiece_ReturnsCorrectNextPiece()
        {
            // Arrange
            var gameManager = new GameManager(12345);
            
            // Act
            int firstPiece = gameManager.GetNextPiece();
            int previewPiece = gameManager.PeekNextPiece();
            int secondPiece = gameManager.GetNextPiece();
            
            // Assert
            Assert.Equal(previewPiece, secondPiece);
        }
        
        [Fact]
        public void GameManager_PeekPreviewPiece_DoesNotAdvanceSequence()
        {
            // Arrange
            var gameManager = new GameManager(12345);
            
            // Act
            int peek1 = gameManager.PeekPreviewPiece();
            int peek2 = gameManager.PeekPreviewPiece();
            
            // Assert - peeking should return same value
            Assert.Equal(peek1, peek2);
        }
        
        [Fact]
        public void TetrisEngine_SetNextPiece_UpdatesPreview()
        {
            // Arrange
            var engine = new TetrisEngine(12345);
            var originalNext = engine.Next.Type;
            
            // Act
            engine.SetNextPiece(TetrominoType.I);
            
            // Assert
            Assert.Equal(TetrominoType.I, engine.Next.Type);
            Assert.NotEqual(originalNext, engine.Next.Type);
        }
        
        [Fact]
        public void MultipleEngines_WithSynchronizedPreview_ShowSameNextPiece()
        {
            // Arrange
            var engine1 = new TetrisEngine();
            var engine2 = new TetrisEngine();
            var gameManager = new GameManager(12345);
            
            // Act - simulate synchronized preview setting
            int previewPieceId = gameManager.PeekPreviewPiece();
            engine1.SetNextPiece((TetrominoType)previewPieceId);
            engine2.SetNextPiece((TetrominoType)previewPieceId);
            
            // Assert
            Assert.Equal(engine1.Next.Type, engine2.Next.Type);
        }
    }
}