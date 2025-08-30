using System;
using System.Threading;
using System.Threading.Tasks;
using TetrisMultiplayer.Networking;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class HostDisconnectTests
    {
        [Fact]
        public void NetworkManager_HasHostDisconnectProperties()
        {
            // Test that the new host disconnect detection properties exist
            var networkManager = new NetworkManager();
            
            // The property should initially be false
            Assert.False(networkManager.IsHostDisconnected);
            
            Console.WriteLine("✓ IsHostDisconnected property exists and returns false initially");
        }
        
        [Fact]
        public async Task NetworkManager_CheckForHostDisconnectAsync_ReturnsInitialState()
        {
            // Test that CheckForHostDisconnectAsync method exists and returns false initially
            var networkManager = new NetworkManager();
            
            var result = await networkManager.CheckForHostDisconnectAsync();
            Assert.False(result);
            
            Console.WriteLine("✓ CheckForHostDisconnectAsync method exists and returns false initially");
        }
        
        [Fact]
        public void NetworkManager_HostDisconnectDetection_MethodsExist()
        {
            // Test that the new methods exist and are callable
            var networkManager = new NetworkManager();
            
            // Should not throw exceptions
            var checkTask = networkManager.CheckForHostDisconnectAsync();
            Assert.NotNull(checkTask);
            Assert.True(checkTask.IsCompleted); // Should complete immediately with no connection
            
            var result = checkTask.Result;
            Assert.False(result); // Should be false when not connected
            
            Console.WriteLine("✓ Host disconnect detection methods exist and are callable");
        }
    }
}