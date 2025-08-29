using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace TetrisMultiplayer.Tests
{
    public class LobbyFlowTests
    {
        [Fact]
        public async Task Lobby_Starts_And_Updates()
        {
            var network = new NetworkManager();
            var cts = new CancellationTokenSource();
            await network.StartHost(6000, cts.Token);
            // Simuliere einen Client-Connect
            var client = new NetworkManager();
            await client.ConnectToHost("127.0.0.1", 6000, "TestUser", cts.Token);
            // Host sollte einen Player haben
            Assert.Single(network.ConnectedPlayerIds);
            // Broadcast LobbyUpdate
            await Program.BroadcastLobbyUpdate(network);
            // (Test: keine Exception)
            cts.Cancel();
        }
    }
}
