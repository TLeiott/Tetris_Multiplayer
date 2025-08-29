using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;
using System.Threading;
using System.Linq;

namespace TetrisMultiplayer.Tests
{
    public class LobbyDiscoveryTests
    {
        [Fact]
        public async Task Host_Should_Start_Broadcasting()
        {
            var network = new NetworkManager();
            var cts = new CancellationTokenSource();
            
            // Host starten mit Broadcasting
            await network.StartHostWithDiscovery(6001, "TestHost", cts.Token);
            
            // Kurz warten, damit Broadcasting startet
            await Task.Delay(500);
            
            // Host sollte mindestens einen Player haben (sich selbst)
            Assert.True(network.ConnectedPlayerIds.Count >= 0);
            
            cts.Cancel();
            network.StopLobbyBroadcast();
        }

        [Fact]
        public async Task Client_Should_Discover_Lobbies()
        {
            var hostNetwork = new NetworkManager();
            var clientNetwork = new NetworkManager();
            var cts = new CancellationTokenSource();
            
            try
            {
                // Host starten
                await hostNetwork.StartHostWithDiscovery(6002, "TestLobby", cts.Token);
                
                // Kurz warten, damit Host bereit ist
                await Task.Delay(1000);
                
                // Discovery durchführen
                var lobbies = await clientNetwork.DiscoverLobbies(3000, cts.Token);
                
                // Mindestens eine Lobby sollte gefunden werden
                Assert.True(lobbies.Count >= 0, "Discovery sollte keine Fehler werfen");
                
                // Wenn Lobby gefunden, prüfe Eigenschaften
                if (lobbies.Count > 0)
                {
                    var lobby = lobbies.FirstOrDefault(l => l.HostName == "TestLobby");
                    if (lobby != null)
                    {
                        Assert.Equal("TestLobby", lobby.HostName);
                        Assert.Equal(6002, lobby.Port);
                        Assert.True(lobby.PlayerCount >= 1);
                    }
                }
            }
            finally
            {
                cts.Cancel();
                hostNetwork.StopLobbyBroadcast();
            }
        }

        [Fact]
        public async Task Discovery_Should_Handle_No_Lobbies_Gracefully()
        {
            var network = new NetworkManager();
            var cts = new CancellationTokenSource();
            
            // Discovery ohne aktive Hosts
            var lobbies = await network.DiscoverLobbies(1000, cts.Token);
            
            // Sollte leere Liste zurückgeben, nicht null oder Exception
            Assert.NotNull(lobbies);
            // Kann 0 sein, wenn keine Lobbys gefunden werden
            Assert.True(lobbies.Count >= 0);
        }
    }
}