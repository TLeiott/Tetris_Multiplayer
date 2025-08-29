using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;
using TetrisMultiplayer.Game;
using System.Linq;

namespace TetrisMultiplayer.Tests
{
    public class IntegrationTest
    {
        [Fact]
        public async Task HostAndTwoClients_PlayThreeRounds()
        {
            var cts = new CancellationTokenSource();
            var host = new NetworkManager();
            await host.StartHost(7000, cts.Token);
            var client1 = new NetworkManager();
            var client2 = new NetworkManager();
            await client1.ConnectToHost("127.0.0.1", 7000, "TestUser1", cts.Token);
            await client2.ConnectToHost("127.0.0.1", 7000, "TestUser2", cts.Token);
            // Simuliere 3 Runden: Host sendet NextPiece, Clients senden PlacedPiece
            for (int round = 0; round < 3; round++)
            {
                int pieceId = round % 7;
                var nextPiece = new { type = "NextPiece", pieceId };
                await host.BroadcastAsync(nextPiece);
                // Clients empfangen und senden PlacedPiece
                // (Hier: Dummy-Implementierung, da kein echtes Spielfeld)
                var placedMsg1 = new { type = "PlacedPiece", playerId = "p1", pieceId, placedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), locks = true };
                var placedMsg2 = new { type = "PlacedPiece", playerId = "p2", pieceId, placedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), locks = true };
                await host.BroadcastAsync(placedMsg1);
                await host.BroadcastAsync(placedMsg2);
            }
            // Test: keine Exception, Verbindungen bestehen
            Assert.True(host.ConnectedPlayerIds.Count >= 2);
            cts.Cancel();
        }
    }
}
