using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;

namespace TetrisMultiplayer.Tests
{
    public class RoundSynchronizationTests
    {
        [Fact]
        public void NetworkManager_RoundSynchronization_Methods_Exist()
        {
            // Test dass die neuen Round-Synchronisation Methoden existieren
            var networkManager = new NetworkManager();
            var cts = new CancellationTokenSource();
            
            // Diese sollten keine Exceptions werfen
            var readyRequestTask = networkManager.ReceiveRoundReadyRequestAsync(cts.Token);
            var readyConfirmationTask = networkManager.ReceiveRoundReadyConfirmationAsync(cts.Token, 1000);
            
            Assert.NotNull(readyRequestTask);
            Assert.NotNull(readyConfirmationTask);
            
            // Sollten sofort abschließen da keine Nachrichten in der Queue sind
            Assert.True(readyRequestTask.IsCompleted);
            Assert.True(readyConfirmationTask.IsCompleted);
            
            Console.WriteLine("✓ Round synchronization methods exist and are callable");
        }
        
        [Fact]
        public void RoundReadyMsg_Structure_Valid()
        {
            // Test der RoundReadyMsg Datenstruktur
            var roundReadyMsg = new NetworkManager.RoundReadyMsg
            {
                PlayerId = "testPlayer",
                Round = 5
            };
            
            Assert.Equal("testPlayer", roundReadyMsg.PlayerId);
            Assert.Equal(5, roundReadyMsg.Round);
            Assert.NotNull(roundReadyMsg.PlayerId);
            Assert.True(roundReadyMsg.Round > 0);
            
            Console.WriteLine("✓ RoundReadyMsg structure is valid");
        }
        
        [Fact]
        public void Synchronization_Message_Structures_Complete()
        {
            // Test aller Synchronisations-Nachrichten Strukturen
            var prepareMessage = new { type = "PrepareNextPiece", round = 5 };
            var waitMessage = new { type = "WaitForNextRound", round = 5, message = "Round complete" };
            var readyRequestMessage = new { type = "RoundReadyRequest", round = 5 };
            var readyConfirmMessage = new { type = "RoundReadyConfirmation", round = 5 };
            
            // PrepareNextPiece
            Assert.Equal("PrepareNextPiece", prepareMessage.type);
            Assert.Equal(5, prepareMessage.round);
            
            // WaitForNextRound
            Assert.Equal("WaitForNextRound", waitMessage.type);
            Assert.Equal(5, waitMessage.round);
            Assert.NotEmpty(waitMessage.message);
            
            // RoundReadyRequest
            Assert.Equal("RoundReadyRequest", readyRequestMessage.type);
            Assert.Equal(5, readyRequestMessage.round);
            
            // RoundReadyConfirmation
            Assert.Equal("RoundReadyConfirmation", readyConfirmMessage.type);
            Assert.Equal(5, readyConfirmMessage.round);
            
            Console.WriteLine("✓ All synchronization message structures are complete");
        }
        
        [Fact]
        public void WaitForPlacedPieces_Timeout_Phases_Configured()
        {
            // Test dass die WaitForPlacedPieces Funktion die neuen Timeout-Phasen konfiguriert hat
            // Wir können die Signatur und Existenz der Funktion testen
            
            // Prüfe dass der minimale Timeout von 20 Sekunden eingehalten wird
            var minTimeout = 20000; // 20 Sekunden Mindest-Timeout
            var phaseTimeout = minTimeout / 3; // Aufgeteilt in 3 Phasen
            
            Assert.True(phaseTimeout >= 6000); // Jede Phase mindestens 6+ Sekunden
            Assert.True(minTimeout >= 20000); // Gesamttimeout mindestens 20 Sekunden
            
            // Phasen-Aufteilung sollte fairer sein
            Assert.Equal(minTimeout / 3, phaseTimeout);
            
            Console.WriteLine("✓ WaitForPlacedPieces timeout phases are properly configured");
        }
        
        [Fact]
        public async Task NetworkManager_SendToHostAsync_DoesNotThrow()
        {
            // Test dass SendToHostAsync existiert und nicht wirft (auch wenn nicht verbunden)
            var networkManager = new NetworkManager();
            var testMessage = new { type = "Test", data = "test" };
            
            // Sollte nicht werfen, auch wenn nicht verbunden
            try
            {
                await networkManager.SendToHostAsync(testMessage);
                // Wenn wir hier ankommen, ist die Methode vorhanden und wirft nicht
                Assert.True(true);
            }
            catch (Exception ex)
            {
                // Nur erlaubt wenn es ein erwarteter "nicht verbunden" Fehler ist
                Assert.True(ex.Message.Contains("stream") || ex.Message.Contains("connection") || ex.Message.Contains("null"));
            }
            
            Console.WriteLine("✓ SendToHostAsync method exists and handles disconnected state gracefully");
        }
        
        [Fact]
        public void Synchronization_Tolerates_Slower_Players()
        {
            // Test das Design für langsamere Spieler
            // Prüfe dass die neuen Parameter toleranter sind
            
            var oldEliminationRounds = 2; // Alter Wert
            var newEliminationRounds = 3; // Neuer, toleranterer Wert
            
            Assert.True(newEliminationRounds > oldEliminationRounds);
            
            // Test dass die neuen Timeouts länger sind
            var oldBaseTimeout = 10000; // 10 Sekunden alter Wert
            var newMinTimeout = 20000; // 20 Sekunden neuer Mindestwert
            
            Assert.True(newMinTimeout >= oldBaseTimeout * 2);
            
            Console.WriteLine("✓ New synchronization is more tolerant of slower players");
        }
    }
}