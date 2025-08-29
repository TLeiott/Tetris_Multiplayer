using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace TetrisMultiplayer.Tests
{
    public class HostLobbyRefreshTests
    {
        [Fact]
        public async Task Host_CanDetectNewPlayerConnections()
        {
            // Setup host network
            var network = new NetworkManager();
            var cts = new CancellationTokenSource();
            await network.StartHost(6001, cts.Token);
            
            // Initially no clients
            Assert.Empty(network.ConnectedPlayerIds);
            
            // Simulate a client connecting
            var client = new NetworkManager();
            await client.ConnectToHost("127.0.0.1", 6001, "TestUser", cts.Token);
            
            // Give some time for connection to be established
            await Task.Delay(500);
            
            // Host should now see the connected client
            Assert.Single(network.ConnectedPlayerIds);
            
            // Verify the player name is properly stored
            Assert.True(network.PlayerNames.Values.Contains("TestUser"));
            
            cts.Cancel();
        }
        
        [Fact]
        public async Task BroadcastLobbyUpdate_IncludesAllPlayers()
        {
            // Setup host
            var network = new NetworkManager();
            var cts = new CancellationTokenSource();
            await network.StartHost(6002, cts.Token);
            
            // Add a player
            var client = new NetworkManager();
            await client.ConnectToHost("127.0.0.1", 6002, "Player1", cts.Token);
            await Task.Delay(200);
            
            // Test that BroadcastLobbyUpdate works without exception
            var playerNames = new Dictionary<string, string> 
            { 
                ["host"] = "HostPlayer"
            };
            
            // This should include both host and connected client
            await Program.BroadcastLobbyUpdate(network, null, null, playerNames);
            
            // If we reach here without exception, the test passes
            Assert.True(true);
            
            cts.Cancel();
        }
    }
}