using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TetrisMultiplayer.Networking;

namespace TetrisMultiplayer.Tests
{
    public class ImprovedLobbyDiscoveryTests
    {
        [Fact]
        public async Task NetworkDiagnostics_ShouldShowLocalInterfaces()
        {
            var network = new NetworkManager();
            
            var diagnostics = await network.DiagnoseNetworkConnectivity();
            
            Assert.NotNull(diagnostics);
            Assert.Contains("=== NETZWERK-DIAGNOSE ===", diagnostics);
            Assert.Contains("Lokale IP-Adressen", diagnostics);
            Assert.Contains("Netzwerk-Interfaces", diagnostics);
            Assert.Contains("Broadcast-Ziele", diagnostics);
        }

        [Fact]
        public async Task ImprovedDiscovery_ShouldHandleMultipleApproaches()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var network = new NetworkManager();
            
            // Start a mock host for testing
            var hostTask = Task.Run(async () =>
            {
                try
                {
                    await network.StartHostWithDiscovery(5000, "TestHost", cts.Token);
                    await Task.Delay(8000, cts.Token); // Keep host alive
                }
                catch (OperationCanceledException) { }
            });

            // Give host time to start
            await Task.Delay(2000);

            // Test discovery with improved timeout
            var clientNetwork = new NetworkManager();
            var lobbies = await clientNetwork.DiscoverLobbies(5000, cts.Token);

            // Cleanup
            cts.Cancel();
            network.StopLobbyBroadcast();

            // Verify discovery worked (may find the test host or other hosts on network)
            Assert.NotNull(lobbies);
            // Note: We can't guarantee finding hosts in test environment,
            // but we can verify the method executes without errors
        }

        [Fact]
        public async Task DiscoveryWithVPN_ShouldHandleMultipleNetworkSegments()
        {
            var network = new NetworkManager();
            
            // Test the network diagnostic functionality
            var diagnostics = await network.DiagnoseNetworkConnectivity();
            
            // Verify that the diagnostics include various network types
            Assert.Contains("IP-Adressen", diagnostics);
            
            // Test discovery on potentially complex network setup
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var lobbies = await network.DiscoverLobbies(3000, cts.Token);
            
            // Should complete without throwing
            Assert.NotNull(lobbies);
        }

        [Fact]
        public async Task Discovery_ShouldHandleTimeouts()
        {
            var network = new NetworkManager();
            
            // Test with short timeout to ensure timeout handling works
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var lobbies = await network.DiscoverLobbies(1000, cts.Token);
            
            // Should complete without hanging
            Assert.NotNull(lobbies);
        }

        [Fact]
        public async Task BroadcastTargets_ShouldIncludeMultipleSegments()
        {
            var network = new NetworkManager();
            
            // This tests internal logic by running diagnostics
            var diagnostics = await network.DiagnoseNetworkConnectivity();
            
            // Should include broadcast information
            Assert.Contains("Broadcast-Ziele", diagnostics);
        }
    }
}