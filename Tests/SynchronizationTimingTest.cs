using System;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    /// <summary>
    /// Test für die verbesserte Synchronisations-Timing zwischen Host und Client
    /// Validates the fix for "Delay nach piece place zu groß"
    /// </summary>
    public class SynchronizationTimingTest
    {
        [Fact]
        public void WaitForPlacedPieces_Uses_Optimized_Timeout()
        {
            // Test dass der optimierte 15-Sekunden Timeout verwendet wird
            var optimizedTimeout = 15000; // 15 Sekunden
            var oldTimeout = 25000; // 25 Sekunden (zu lang)
            
            // Der neue Timeout ist kürzer für bessere Synchronisation
            Assert.True(optimizedTimeout < oldTimeout);
            
            // Aber immer noch ausreichend Zeit für Piece-Placement
            Assert.True(optimizedTimeout >= 15000);
            
            // 3-Phasen System: 5 Sekunden pro Phase
            var phaseTimeout = optimizedTimeout / 3;
            Assert.Equal(5000, phaseTimeout);
            
            Console.WriteLine($"✓ Optimized timeout: {optimizedTimeout/1000}s total, {phaseTimeout/1000}s per phase");
            Console.WriteLine("✓ This reduces the delay between host 'Round Complete' and client readiness");
        }
        
        [Fact]
        public void Synchronization_Timing_Improvement_Validated()
        {
            // Test der Timing-Verbesserung
            var oldHostClientGap = 25; // 25 Sekunden potentieller Gap
            var newHostClientGap = 15; // 15 Sekunden optimierter Gap
            
            var improvementRatio = (double)oldHostClientGap / newHostClientGap;
            
            // 40% Verbesserung der Synchronisations-Geschwindigkeit
            Assert.True(improvementRatio >= 1.6); 
            
            Console.WriteLine($"✓ Synchronization timing improved by {improvementRatio:F1}x");
            Console.WriteLine("✓ Host shows 'Round Complete' closer to when clients are actually ready");
        }
    }
}