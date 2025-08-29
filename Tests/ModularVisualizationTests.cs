using System;
using System.Collections.Generic;
using TetrisMultiplayer.Game;
using TetrisMultiplayer.UI;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class ModularVisualizationTests
    {
        [Fact]
        public void TestPieceBoundsCalculation()
        {
            // Test I-piece bounds calculation
            var bounds = ConsoleUI.PieceVisualizationHelper.GetPieceBounds(TetrominoType.I, 0);
            Assert.Equal(4, bounds.width); // I-piece is 4 wide horizontally
            Assert.Equal(1, bounds.height); // I-piece is 1 high horizontally
            
            bounds = ConsoleUI.PieceVisualizationHelper.GetPieceBounds(TetrominoType.I, 1);
            Assert.Equal(1, bounds.width); // I-piece is 1 wide vertically
            Assert.Equal(4, bounds.height); // I-piece is 4 high vertically
        }
        
        [Fact]
        public void TestOptimalPreviewSize()
        {
            // Test that I-piece gets proper preview size
            int iPreviewSize = ConsoleUI.PieceVisualizationHelper.GetOptimalPreviewSize(TetrominoType.I);
            Assert.True(iPreviewSize >= 4, "I-piece preview should be at least 4 to accommodate its length");
            
            // Test other pieces
            int oPreviewSize = ConsoleUI.PieceVisualizationHelper.GetOptimalPreviewSize(TetrominoType.O);
            Assert.True(oPreviewSize >= 2, "O-piece preview should accommodate 2x2 size");
            
            // All pieces should get reasonable preview sizes
            foreach (TetrominoType type in Enum.GetValues<TetrominoType>())
            {
                int size = ConsoleUI.PieceVisualizationHelper.GetOptimalPreviewSize(type);
                Assert.True(size >= 2 && size <= 6, $"Piece {type} should have reasonable preview size, got {size}");
            }
        }
        
        [Fact]
        public void TestOptimalCenterPosition()
        {
            // Test that centering works for all pieces
            foreach (TetrominoType type in Enum.GetValues<TetrominoType>())
            {
                int previewSize = ConsoleUI.PieceVisualizationHelper.GetOptimalPreviewSize(type);
                var (centerX, centerY) = ConsoleUI.PieceVisualizationHelper.GetOptimalCenterPosition(type, previewSize, 0);
                
                // Verify piece blocks fit within preview grid when centered
                var piece = new Tetromino(type);
                foreach (var (x, y) in piece.Blocks(centerX, centerY, 0))
                {
                    Assert.True(x >= 0 && x < previewSize, 
                        $"Piece {type} block at ({x},{y}) should be within preview grid [0,{previewSize})");
                    Assert.True(y >= 0 && y < previewSize, 
                        $"Piece {type} block at ({x},{y}) should be within preview grid [0,{previewSize})");
                }
            }
        }
        
        [Fact]
        public void TestModularityForFutureExpansion()
        {
            // This test ensures the system can handle different piece types modularly
            // We test that the helper methods work consistently for all current piece types
            
            var testedTypes = new List<TetrominoType>();
            foreach (TetrominoType type in Enum.GetValues<TetrominoType>())
            {
                testedTypes.Add(type);
                
                // Each piece should have valid bounds in all rotations
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var bounds = ConsoleUI.PieceVisualizationHelper.GetPieceBounds(type, rotation);
                    Assert.True(bounds.width > 0 && bounds.height > 0, 
                        $"Piece {type} rotation {rotation} should have positive dimensions");
                }
                
                // Each piece should get a valid preview size
                int previewSize = ConsoleUI.PieceVisualizationHelper.GetOptimalPreviewSize(type);
                Assert.True(previewSize > 0, $"Piece {type} should have positive preview size");
                
                // Each piece should get valid center positions
                var (centerX, centerY) = ConsoleUI.PieceVisualizationHelper.GetOptimalCenterPosition(type, previewSize, 0);
                Assert.True(centerX >= 0 && centerY >= 0, 
                    $"Piece {type} should have non-negative center position");
            }
            
            // Verify we tested all expected piece types
            Assert.Equal(7, testedTypes.Count); // I, O, T, S, Z, J, L
        }
    }
}